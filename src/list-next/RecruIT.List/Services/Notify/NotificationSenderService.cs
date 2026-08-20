using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mjml.Net;
using RecruIT.List.Data;
using RecruIT.List.Data.Entities;
using RecruIT.List.Models;
using RecruIT.List.Options;
using RecruIT.List.Services.Access;
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
/// the same subscriber between ticks become one digest rather than several. Subscriptions whose
/// trial access has since been revoked are skipped and cleaned up the same way as ones that were
/// unsubscribed - see TrialAccessService.GetGrantedPairsAsync (no IsAdmin bypass there; see its
/// doc comment).
/// </summary>
public sealed class NotificationSenderService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ScreeningListService screeningListService,
    TrialAccessService accessService,
    IOptions<NotifyMailerOptions> mailerOptions,
    INotificationChannel channel,
    ILogger<NotificationSenderService> logger
)
{
    private static readonly MjmlRenderer Renderer = new();

    /// <summary>
    /// A delivery that has failed to send for longer than this is given up on (removed) rather
    /// than retried forever - generous enough to comfortably outlast a Monthly subscriber's own
    /// cadence plus a prolonged SMTP outage, while still bounding retries for a permanently-bad
    /// address.
    /// </summary>
    private static readonly TimeSpan MaxDeliveryAge = TimeSpan.FromDays(30);

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

        // One grouped query for every pending subscriber's last-sent time, instead of one query
        // per subscriber inside the loop below.
        var lastSentBySubscription = await db
            .NotificationDeliveries.Where(d =>
                pendingSubscriptionIds.Contains(d.NotificationSubscriptionId)
                && d.Channel == NotificationChannel.Email
                && d.SentAt != null
            )
            .GroupBy(d => d.NotificationSubscriptionId)
            .Select(g => new { SubscriptionId = g.Key, LastSentAt = g.Max(d => d.SentAt) })
            .ToDictionaryAsync(x => x.SubscriptionId, x => x.LastSentAt, ct);

        var grantedPairs = await accessService.GetGrantedPairsAsync(
            subscriptionsById.Values.Select(s => s.SubjectId).Distinct().ToList(),
            subscriptionsById.Values.Select(s => s.Email).Distinct().ToList(),
            ct
        );

        var toCleanUpSubscriptionIds = new List<Guid>();
        var dueSubscriptionIds = new List<Guid>();
        foreach (var subscriptionId in pendingSubscriptionIds)
        {
            if (!subscriptionsById.TryGetValue(subscriptionId, out var subscription))
            {
                // Unsubscribed since these were queued - don't email them, just clean up rather
                // than leaving them to be re-checked (and re-orphaned) forever.
                toCleanUpSubscriptionIds.Add(subscriptionId);
                continue;
            }

            var hasAccess =
                grantedPairs.Contains((subscription.SubjectId, subscription.TrialIdentifier))
                || grantedPairs.Contains((subscription.Email, subscription.TrialIdentifier));
            if (!hasAccess)
            {
                // Trial access was revoked since these were queued - same cleanup as an
                // unsubscribe for the pending deliveries, but the subscription row itself is left
                // alone in case access is restored later.
                toCleanUpSubscriptionIds.Add(subscriptionId);
                continue;
            }

            if (subscription.Frequency != NotificationFrequency.Instant)
            {
                var lastSent =
                    lastSentBySubscription.GetValueOrDefault(subscriptionId)
                    ?? subscription.CreatedAt;

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

        if (toCleanUpSubscriptionIds.Count > 0)
        {
            var toRemove = await db
                .NotificationDeliveries.Where(d =>
                    toCleanUpSubscriptionIds.Contains(d.NotificationSubscriptionId)
                    && d.Channel == NotificationChannel.Email
                    && d.SentAt == null
                )
                .ToListAsync(ct);
            db.NotificationDeliveries.RemoveRange(toRemove);
        }

        if (dueSubscriptionIds.Count == 0)
        {
            await db.SaveChangesAsync(ct); // persists any cleanup above
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
        var listInfoByTrial = await screeningListService.ResolveListsForTrialsAsync(
            due.Where(d => events.ContainsKey(d.NotificationEventId))
                .Select(d => events[d.NotificationEventId].TrialIdentifier),
            ct
        );

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
                    ) || listInfo is null
                )
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

                var stale = includedDeliveries
                    .Where(d =>
                        events.TryGetValue(d.NotificationEventId, out var e)
                        && now - e.OccurredAt > MaxDeliveryAge
                    )
                    .ToList();
                if (stale.Count > 0)
                {
                    logger.LogWarning(
                        "Giving up on {Count} notification deliveries to {Email} - pending longer than {MaxAgeDays} days with repeated send failures",
                        stale.Count,
                        recipientEmail,
                        MaxDeliveryAge.TotalDays
                    );
                    db.NotificationDeliveries.RemoveRange(stale);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Admin-triggered, one-off send: exercises the exact same SMTP config and MJML template as a
    /// real digest, using one obviously-fake sample item, so an admin can verify NotifyMailerOptions
    /// (SMTP host/credentials, From, subject/link templates) actually works without waiting for a
    /// real detected event or touching any subscription/delivery rows.
    /// </summary>
    public async Task SendTestEmailAsync(string recipientEmail, CancellationToken ct = default)
    {
        var sampleItems = new List<(string StudyAcronym, string PatientDisplayName, string ListUrl)>
        {
            (
                "TEST-TRIAL",
                "Test Patient",
                mailerOptions.Value.ScreeningListLinkTemplate.Replace("[list_id]", "test")
            ),
        };

        var subject =
            "[Test] "
            + mailerOptions.Value.SubjectTemplate.Replace(
                "[study_acronym]",
                sampleItems[0].StudyAcronym
            );
        var html = RenderHtml(sampleItems);

        await channel.SendAsync(recipientEmail, subject, html, ct);
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
