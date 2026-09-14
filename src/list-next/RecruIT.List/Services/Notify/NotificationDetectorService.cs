using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Services.Fhir;
using FhirList = Hl7.Fhir.Model.List;
using ListFhirConstants = RecruIT.List.Services.Fhir.FhirConstants;
using Task = System.Threading.Tasks.Task;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// Recurring, cluster-wide poll trigger (Hangfire guarantees exactly one `list-next` replica fires
/// per tick) - same detection technique as the old ScreeningListPollService (plain FHIR search +
/// vread against `List` version history, via PollCursor and ScreeningListDiff), but instead of
/// delivering immediately, it writes one NotificationEvent per newly-diffed patient and
/// materializes NotificationDelivery rows for every active subscription on that trial in the same
/// pass - see NotificationSenderService for what actually sends the Email rows.
/// </summary>
public sealed class NotificationDetectorService(
    FhirClientFactory clientFactory,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<NotificationDetectorService> logger
)
{
    /// <summary>
    /// The `List?code=...` search for every screening list, regardless of status - built from the
    /// same generated FhirConstants as ScreeningListService.BuildScreeningListsQuery rather than
    /// duplicated as a hand-maintained config value, so it can't drift from the actual code system.
    /// </summary>
    private static readonly string ListSearchCriteria =
        $"List?code={Uri.EscapeDataString($"{ListFhirConstants.SystemScreeningList}|{ListFhirConstants.ScreeningListCode}")}";

    public async Task PollAllTrialsAsync(CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();
        var resources = await FhirBundleHelpers.GetAllPagesAsync(client, ListSearchCriteria, ct);

        // Lists are independent - polled concurrently, each with its own FhirClient (BaseFhirClient
        // tracks last-request state as mutable instance fields, so a shared client isn't safe to
        // call from multiple tasks at once).
        await Task.WhenAll(
            resources.OfType<FhirList>().Select(list => PollListSafeAsync(list, ct))
        );
    }

    private async Task PollListSafeAsync(FhirList list, CancellationToken ct)
    {
        try
        {
            await PollListAsync(clientFactory.CreateClient(), list, ct);
        }
        catch (Exception ex)
        {
            // one misbehaving list must not stop the rest of this tick from being processed.
            logger.LogError(ex, "Failed to poll List/{ListId}", list.Id);
        }
    }

    private async Task PollListAsync(
        Hl7.Fhir.Rest.FhirClient client,
        FhirList current,
        CancellationToken ct
    )
    {
        var listId = current.Id ?? throw new InvalidOperationException("List has no id.");
        var currentVersionId = current.Meta?.VersionId;
        if (currentVersionId is null)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var cursor = await db.PollCursors.FindAsync([listId], ct);
        if (cursor is null)
        {
            // first time seeing this list - baseline it without notifying for its existing entries.
            db.PollCursors.Add(
                new PollCursor
                {
                    ListId = listId,
                    LastSeenVersionId = currentVersionId,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }
            );
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // another replica already bootstrapped it concurrently - fine, ignore.
            }
            return;
        }

        if (cursor.LastSeenVersionId == currentVersionId)
        {
            return;
        }

        var previousVersionId = cursor.LastSeenVersionId;

        // Read the previous version and compute the diff *before* touching the cursor - if this
        // throws (transient network error, or the previous version was pruned server-side), the
        // exception propagates to PollAllTrialsAsync's per-list catch and the cursor is left
        // exactly where it was, so the next tick retries this same diff instead of silently and
        // permanently skipping the patients it would have found.
        var previous = await client.ReadAsync<FhirList>(
            $"List/{listId}/_history/{previousVersionId}",
            ct: ct
        );
        var newReferences = ScreeningListDiff.NewEntryReferences(previous, current);

        cursor.LastSeenVersionId = currentVersionId;
        cursor.UpdatedAt = DateTimeOffset.UtcNow;

        if (newReferences.Count > 0)
        {
            await QueueNotificationsAsync(
                client,
                db,
                listId,
                current,
                currentVersionId,
                newReferences,
                ct
            );
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // another replica already claimed this version transition this tick - discard our
            // in-memory cursor/event/delivery changes and let them handle it.
        }
        catch (DbUpdateException)
        {
            // another replica already recorded these exact events (DedupeKey collision) - fine, skip.
        }
    }

    /// <summary>
    /// Resolves the trial for `current` and, if resolvable, writes one NotificationEvent per
    /// newly-diffed patient plus one NotificationDelivery per (event, subscription, channel) -
    /// tracked on `db` but not saved here, so the caller can commit them atomically together with
    /// the cursor advance that made this diff possible.
    /// </summary>
    private async Task QueueNotificationsAsync(
        Hl7.Fhir.Rest.FhirClient client,
        AppDbContext db,
        string listId,
        FhirList current,
        string currentVersionId,
        IReadOnlyList<string> newReferences,
        CancellationToken ct
    )
    {
        var studyId = current
            .GetReferenceExtension(ListFhirConstants.UrlListBelongsToStudy)
            ?.GetReferencedId();
        var study = studyId is not null
            ? await client.ReadAsync<ResearchStudy>($"ResearchStudy/{studyId}", ct: ct)
            : null;

        var trialIdentifier = study?.GetTrialIdentifier();
        if (trialIdentifier is null)
        {
            logger.LogWarning(
                "List/{ListId} has no resolvable trial identifier - skipping {Count} new entries",
                listId,
                newReferences.Count
            );
            return;
        }

        var token = trialIdentifier.ToToken();
        var subscriptions = await db
            .NotificationSubscriptions.AsNoTracking()
            .Where(s => s.TrialIdentifier == token)
            .ToListAsync(ct);

        // Independent per-reference resolutions (each two sequential FHIR reads) - run concurrently,
        // each with its own FhirClient rather than the shared `client` above (see PollAllTrialsAsync
        // for why a FhirClient can't safely be called from multiple tasks at once).
        var displayNames = await Task.WhenAll(
            newReferences.Select(reference => ResolvePatientDisplayNameAsync(reference, ct))
        );

        for (var i = 0; i < newReferences.Count; i++)
        {
            var reference = newReferences[i];
            var notificationEvent = new NotificationEvent
            {
                Id = Guid.NewGuid(),
                TrialIdentifier = token,
                PatientReference = reference,
                PatientDisplayName = displayNames[i],
                OccurredAt = DateTimeOffset.UtcNow,
                DedupeKey = $"{token}:{currentVersionId}:{reference}",
            };
            db.NotificationEvents.Add(notificationEvent);

            foreach (var subscription in subscriptions)
            {
                foreach (var channel in DeliveryChannels)
                {
                    db.NotificationDeliveries.Add(
                        new NotificationDelivery
                        {
                            Id = Guid.NewGuid(),
                            NotificationEventId = notificationEvent.Id,
                            NotificationSubscriptionId = subscription.Id,
                            SubjectId = subscription.SubjectId,
                            Email = subscription.Email,
                            Channel = channel,
                            ScheduledFor = notificationEvent.OccurredAt,
                        }
                    );
                }
            }
        }
    }

    private static readonly NotificationChannel[] DeliveryChannels =
    [
        NotificationChannel.InApp,
        NotificationChannel.Email,
    ];

    private async Task<string> ResolvePatientDisplayNameAsync(
        string researchSubjectReference,
        CancellationToken ct
    )
    {
        try
        {
            var subjectId = researchSubjectReference.Split('/').LastOrDefault();
            if (subjectId is null)
            {
                return "Unknown patient";
            }

            var client = clientFactory.CreateClient();
            var subject = await client.ReadAsync<ResearchSubject>(
                $"ResearchSubject/{subjectId}",
                ct: ct
            );
            var patientId = subject?.Individual.GetReferencedId();
            if (patientId is null)
            {
                return "Unknown patient";
            }

            var patient = await client.ReadAsync<Patient>($"Patient/{patientId}", ct: ct);
            return ScreeningListService.FormatPatientName(patient) ?? patientId;
        }
        catch (Exception)
        {
            // Best-effort - a generic display name is preferable to blocking event detection on a
            // single patient/subject read failure.
            return "Unknown patient";
        }
    }
}
