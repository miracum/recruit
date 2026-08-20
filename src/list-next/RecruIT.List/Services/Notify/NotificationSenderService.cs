using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mjml.Net;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Options;
using RecruIT.List.Services.Fhir;
using Task = System.Threading.Tasks.Task;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// Recurring job: for every subscription with at least one pending (SentAt still null) Email
/// NotificationDelivery, checks whether that subscription is due a send *right now* - Instant
/// always is; Daily/Weekly/Monthly are checked against the subscription's current
/// Frequency/DayOfWeek/TimeOfDay/TimeZoneId via NotificationScheduling, using the last time this
/// subscription was actually sent (or its CreatedAt, if never sent) as the reference point. Due
/// subscriptions get every pending delivery sent as one grouped email - several events queued for
/// the same subscriber between ticks become one digest rather than several.
/// </summary>
public sealed class NotificationSenderService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ScreeningListService screeningListService,
    IOptions<NotifyMailerOptions> mailerOptions,
    INotificationChannel channel,
    ILogger<NotificationSenderService> logger
)
{
    private static readonly MjmlRenderer Renderer = new();

    public async Task SendDueAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var pendingSubscriptionIds = await db
            .NotificationDeliveries.Where(d =>
                d.Channel == NotificationChannel.Email && d.SentAt == null
            )
            .Select(d => d.NotificationSubscriptionId)
            .Distinct()
            .ToListAsync(ct);

        if (pendingSubscriptionIds.Count == 0)
        {
            return;
        }

        var subscriptionsById = await db
            .NotificationSubscriptions.Where(s => pendingSubscriptionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var dueSubscriptionIds = new List<Guid>();
        foreach (var subscriptionId in pendingSubscriptionIds)
        {
            if (!subscriptionsById.TryGetValue(subscriptionId, out var subscription))
            {
                // Unsubscribed since these were queued - don't email them, just clean up rather
                // than leaving them to be re-checked (and re-orphaned) forever.
                var orphaned = await db
                    .NotificationDeliveries.Where(d =>
                        d.NotificationSubscriptionId == subscriptionId
                        && d.Channel == NotificationChannel.Email
                        && d.SentAt == null
                    )
                    .ToListAsync(ct);
                db.NotificationDeliveries.RemoveRange(orphaned);
                continue;
            }

            if (subscription.Frequency != NotificationFrequency.Instant)
            {
                var lastSent =
                    await db
                        .NotificationDeliveries.Where(d =>
                            d.NotificationSubscriptionId == subscriptionId
                            && d.Channel == NotificationChannel.Email
                            && d.SentAt != null
                        )
                        .MaxAsync(d => (DateTimeOffset?)d.SentAt, ct) ?? subscription.CreatedAt;

                var nextSlot = NotificationScheduling.ComputeEmailScheduledFor(
                    subscription.Frequency,
                    subscription.DayOfWeek,
                    subscription.TimeOfDay,
                    subscription.TimeZoneId,
                    lastSent
                );

                if (nextSlot > now)
                {
                    continue;
                }
            }

            dueSubscriptionIds.Add(subscriptionId);
        }

        if (dueSubscriptionIds.Count == 0)
        {
            await db.SaveChangesAsync(ct); // persists any orphan cleanup above
            return;
        }

        var due = await db
            .NotificationDeliveries.Where(d =>
                d.Channel == NotificationChannel.Email
                && d.SentAt == null
                && dueSubscriptionIds.Contains(d.NotificationSubscriptionId)
            )
            .ToListAsync(ct);

        var eventIds = due.Select(d => d.NotificationEventId).Distinct().ToList();
        var events = await db
            .NotificationEvents.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        // Several events commonly share a trial (and several deliveries across groups commonly
        // share an event's trial too) - resolve each trial's current List id/acronym at most once
        // for this whole tick rather than once per delivery.
        var listInfoByTrial = new Dictionary<string, (string ListId, string StudyAcronym)?>();

        foreach (var group in due.GroupBy(d => d.NotificationSubscriptionId))
        {
            var deliveries = group.ToList();
            var recipientEmail = deliveries[0].Email;

            var items =
                new List<(string StudyAcronym, string PatientDisplayName, string ListUrl)>();
            var includedDeliveries = new List<NotificationDelivery>();
            foreach (var delivery in deliveries)
            {
                if (!events.TryGetValue(delivery.NotificationEventId, out var notificationEvent))
                {
                    continue;
                }

                if (
                    !listInfoByTrial.TryGetValue(
                        notificationEvent.TrialIdentifier,
                        out var listInfo
                    )
                )
                {
                    listInfo = await screeningListService.ResolveListForTrialAsync(
                        notificationEvent.TrialIdentifier,
                        ct
                    );
                    listInfoByTrial[notificationEvent.TrialIdentifier] = listInfo;
                }

                if (listInfo is null)
                {
                    logger.LogWarning(
                        "Could not resolve a screening list for trial {TrialIdentifier} - skipping delivery {DeliveryId} this tick",
                        notificationEvent.TrialIdentifier,
                        delivery.Id
                    );
                    continue;
                }

                items.Add(
                    (
                        listInfo.Value.StudyAcronym,
                        notificationEvent.PatientDisplayName,
                        mailerOptions.Value.ScreeningListLinkTemplate.Replace(
                            "[list_id]",
                            listInfo.Value.ListId
                        )
                    )
                );
                includedDeliveries.Add(delivery);
            }

            if (items.Count == 0)
            {
                continue;
            }

            var acronyms = items.Select(i => i.StudyAcronym).Distinct().ToList();
            var subjectAcronym = acronyms.Count == 1 ? acronyms[0] : $"{acronyms.Count} trials";
            var subject = mailerOptions.Value.SubjectTemplate.Replace(
                "[study_acronym]",
                subjectAcronym
            );
            var html = RenderHtml(items);

            try
            {
                await channel.SendAsync(recipientEmail, subject, html, ct);
                var sentAt = DateTimeOffset.UtcNow;
                foreach (var delivery in includedDeliveries)
                {
                    delivery.SentAt = sentAt;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send notification email to {Email} for {Count} deliveries - will retry next tick",
                    recipientEmail,
                    deliveries.Count
                );
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string RenderHtml(
        IReadOnlyList<(string StudyAcronym, string PatientDisplayName, string ListUrl)> items
    )
    {
        var itemsHtml = string.Join(
            "\n",
            items.Select(i =>
                $"""
                    <mj-text font-size="14px" color="#4a4a4a" line-height="1.5">
                      <b>{WebUtility.HtmlEncode(
                        i.PatientDisplayName
                    )}</b> was newly recommended for <b>{WebUtility.HtmlEncode(
                        i.StudyAcronym
                    )}</b> - <a href="{i.ListUrl}">open screening list</a>
                    </mj-text>
                    """
            )
        );

        var mjml = $"""
            <mjml>
              <mj-body background-color="#f4f4f4">
                <mj-section background-color="#ffffff" padding="24px">
                  <mj-column>
                    <mj-text font-size="18px" font-weight="bold" color="#1a1a1a">
                      New patient suggestions
                    </mj-text>
                    {itemsHtml}
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        var result = Renderer.Render(mjml);
        return result.Html;
    }
}
