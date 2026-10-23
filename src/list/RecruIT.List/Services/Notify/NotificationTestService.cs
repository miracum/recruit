using System.Net;
using System.Security.Claims;
using Hangfire;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using RecruIT.List.Data;
using RecruIT.List.Models;
using RecruIT.List.Options;
using RecruIT.List.Resources;
using RecruIT.List.Services.Access;
using RecruIT.List.Services.Fhir;
using FhirList = Hl7.Fhir.Model.List;
using ListFhirConstants = RecruIT.List.Services.Fhir.FhirConstants;
using Task = System.Threading.Tasks.Task;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// The Notification Test page's on-demand triggers, for trying notifications out without waiting
/// for the query module to find a genuinely new candidate. Re-recommending a patient goes through
/// the same FHIR List history and NotificationDetectorService as a real recommendation, rather
/// than writing a NotificationEvent directly, so it exercises the whole detection path. Gated
/// behind NotificationTestingOptions on top of the admin role, since it notifies every subscriber
/// of the trial for real.
/// </summary>
public sealed class NotificationTestService(
    FhirClientFactory clientFactory,
    NotificationDetectorService detector,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IRecurringJobManager recurringJobManager,
    IOptions<NotificationTestingOptions> options,
    IStringLocalizer<SharedResources> localizer
)
{
    /// <summary>
    /// How often re-adding the entry is retried when the List changed in between - in practice
    /// only ever a query module run landing at the same moment.
    /// </summary>
    private const int MaxRestoreAttempts = 3;

    public bool IsAvailable(ClaimsPrincipal user) =>
        options.Value.Enabled && TrialAccessService.IsAdmin(user);

    public async Task<int> CountSubscribersAsync(
        TrialIdentifier trialIdentifier,
        CancellationToken ct = default
    )
    {
        var token = trialIdentifier.ToToken();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.NotificationSubscriptions.CountAsync(s => s.TrialIdentifier == token, ct);
    }

    /// <summary>
    /// Removes the patient's entry from the List and adds it back, then polls the List straight
    /// away - the detector sees the version without them in its history and notifies every
    /// subscriber, exactly as for a patient the query module dropped and later found again.
    /// Returns how many notifications (NotificationEvents) that created - 1, unless something
    /// went wrong on the detector's side. Counted from the database rather than returned by the
    /// detector, since a scheduled tick polling the same List at the same moment may be the one
    /// that records it.
    /// </summary>
    public async Task<int> ReRecommendAsync(
        string listId,
        string researchSubjectId,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        EnsureAvailable(user);

        // Baselines the List's cursor if nothing has polled it yet - otherwise the poll at the end
        // would only baseline it at the re-added version, and notify nobody.
        await detector.PollListAsync(listId, ct);

        var client = clientFactory.CreateClient();
        var list = await ReadListAsync(client, listId, ct);
        var entry =
            list.Entry.FirstOrDefault(e => e.Item.GetReferencedId() == researchSubjectId)
            ?? throw new InvalidOperationException(
                localizer["App.NotificationTest.ReRecommendNotOnList"]
            );
        var reference = entry.Item.Reference!;
        var token = await ResolveTrialTokenAsync(client, list, ct);
        var eventsBefore = await CountEventsAsync(token, reference, ct);

        list.Entry.Remove(entry);
        try
        {
            await client.UpdateAsync(list, versionAware: true, ct);
        }
        catch (FhirOperationException ex) when (IsVersionConflict(ex))
        {
            throw new InvalidOperationException(
                localizer["App.NotificationTest.ReRecommendConflict"],
                ex
            );
        }

        await RestoreEntryAsync(client, listId, entry, ct);
        await detector.PollListAsync(listId, ct);

        return await CountEventsAsync(token, reference, ct) - eventsBefore;
    }

    /// <summary>
    /// Runs the email sender's recurring job now instead of at its next 5-minute tick - through
    /// Hangfire, so it can't overlap with a scheduled run (see NotificationSenderService). Only
    /// sends what's due: Instant subscribers' emails, not Daily/Weekly/Monthly digests early.
    /// </summary>
    public void SendDueEmailsNow(ClaimsPrincipal user)
    {
        EnsureAvailable(user);
        recurringJobManager.Trigger(NotificationSenderService.RecurringJobId);
    }

    private void EnsureAvailable(ClaimsPrincipal user)
    {
        if (!IsAvailable(user))
        {
            throw new UnauthorizedAccessException(localizer["App.Errors.NotAuthorizedAdminOnly"]);
        }
    }

    /// <summary>
    /// Re-adds the entry dated now, as the query module dates a new recommendation - against a
    /// fresh read each attempt, and not at all if a query module run already put the patient
    /// back in the meantime.
    /// </summary>
    private static async Task RestoreEntryAsync(
        FhirClient client,
        string listId,
        FhirList.EntryComponent entry,
        CancellationToken ct
    )
    {
        var researchSubjectId = entry.Item.GetReferencedId();
        entry.DateElement = new FhirDateTime(DateTimeOffset.UtcNow);
        for (var attempt = 1; ; attempt++)
        {
            var list = await ReadListAsync(client, listId, ct);
            if (list.Entry.Any(e => e.Item.GetReferencedId() == researchSubjectId))
            {
                return;
            }

            list.Entry.Add(entry);
            try
            {
                await client.UpdateAsync(list, versionAware: true, ct);
                return;
            }
            catch (FhirOperationException ex)
                when (IsVersionConflict(ex) && attempt < MaxRestoreAttempts) { }
        }
    }

    private static async Task<FhirList> ReadListAsync(
        FhirClient client,
        string listId,
        CancellationToken ct
    ) =>
        await client.ReadAsync<FhirList>($"List/{listId}", ct: ct)
        ?? throw new InvalidOperationException($"List/{listId} not found.");

    /// <summary>The same List -> ResearchStudy -> trial identifier resolution the detector does.</summary>
    private async Task<string> ResolveTrialTokenAsync(
        FhirClient client,
        FhirList list,
        CancellationToken ct
    )
    {
        var studyId = list.GetReferenceExtension(ListFhirConstants.UrlListBelongsToStudy)
            ?.GetReferencedId();
        var study = studyId is not null
            ? await client.ReadAsync<ResearchStudy>($"ResearchStudy/{studyId}", ct: ct)
            : null;
        return study?.GetTrialIdentifier()?.ToToken()
            ?? throw new InvalidOperationException(
                localizer["App.NotificationTest.ReRecommendNoTrial"]
            );
    }

    private async Task<int> CountEventsAsync(
        string trialToken,
        string reference,
        CancellationToken ct
    )
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.NotificationEvents.CountAsync(
            e => e.TrialIdentifier == trialToken && e.PatientReference == reference,
            ct
        );
    }

    /// <summary>
    /// A version-aware update whose If-Match no longer matches - 412 per the FHIR spec, though
    /// HAPI answers 409.
    /// </summary>
    private static bool IsVersionConflict(FhirOperationException ex) =>
        ex.Status is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict;
}
