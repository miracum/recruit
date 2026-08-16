using Hangfire;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Options;
using RecruIT.List.Services.Fhir;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FhirList = Hl7.Fhir.Model.List;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// Recurring, cluster-wide poll trigger (Hangfire guarantees exactly one `list-next` replica fires
/// per tick). Portable by design - detection is a plain FHIR search + vread, not a Subscription, so
/// this works against any FHIR server that implements the base REST API and retains `List` version
/// history (see the notify-next plan's "Data model" section for that trade-off).
/// </summary>
public sealed class ScreeningListPollService(
    FhirClientFactory clientFactory,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<NotifyMailerOptions> mailerOptions,
    IBackgroundJobClient backgroundJobClient,
    ILogger<ScreeningListPollService> logger
)
{
    public async Task PollAllTrialsAsync(CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();
        var resources = await FhirBundleHelpers.GetAllPagesAsync(
            client,
            mailerOptions.Value.ListSearchCriteria,
            ct
        );

        foreach (var list in resources.OfType<FhirList>())
        {
            try
            {
                await PollListAsync(client, list, ct);
            }
            catch (Exception ex)
            {
                // one misbehaving list must not stop the rest of this tick from being processed.
                logger.LogError(ex, "Failed to poll List/{ListId}", list.Id);
            }
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
        cursor.LastSeenVersionId = currentVersionId;
        cursor.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // another replica already claimed this version transition this tick.
            return;
        }

        var previous = await client.ReadAsync<FhirList>(
            $"List/{listId}/_history/{previousVersionId}",
            ct: ct
        );

        var newReferences = ScreeningListDiff.NewEntryReferences(previous, current);
        foreach (var patientReference in newReferences)
        {
            backgroundJobClient.Enqueue<NotificationDeliveryService>(s =>
                s.DeliverAsync(listId, patientReference, CancellationToken.None)
            );
        }
    }
}
