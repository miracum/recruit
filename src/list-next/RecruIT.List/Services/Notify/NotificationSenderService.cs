using System.Reflection;
using System.Text.Encodings.Web;
using Fluid;
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

    private static readonly TemplateOptions DigestTemplateOptions = CreateDigestTemplateOptions();

    private static readonly IFluidTemplate DigestTemplate = LoadDigestTemplate();

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

            var items = new List<(string StudyAcronym, string ListUrl)>();
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
                        string.Format(
                            mailerOptions.Value.ScreeningListLinkTemplate,
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
            var subject = string.Format(mailerOptions.Value.SubjectTemplate, subjectAcronym);
            var html = RenderHtml(items.Count, subjectAcronym, items[0].ListUrl);

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
        var sampleItems = new List<(string StudyAcronym, string ListUrl)>
        {
            ("TEST-TRIAL", string.Format(mailerOptions.Value.ScreeningListLinkTemplate, "test")),
        };

        var subject =
            "[Test] "
            + string.Format(mailerOptions.Value.SubjectTemplate, sampleItems[0].StudyAcronym);
        var html = RenderHtml(
            sampleItems.Count,
            sampleItems[0].StudyAcronym,
            sampleItems[0].ListUrl
        );

        await channel.SendAsync(recipientEmail, subject, html, ct);
    }

    /// <summary>
    /// Renders the digest email body from an aggregate count only - no patient names or other
    /// per-subject data is ever passed to the template, since a subscriber must not learn who was
    /// suggested from the notification email itself (data protection).
    /// </summary>
    private static string RenderHtml(
        int numNewSubjects,
        string studyAcronym,
        string screeningListLink
    )
    {
        var model = new DigestTemplateModel(numNewSubjects, studyAcronym, screeningListLink);
        var context = new TemplateContext(model, DigestTemplateOptions);
        var mjml = DigestTemplate.Render(context, HtmlEncoder.Default);

        var result = Renderer.Render(mjml);
        return result.Html;
    }

    private static TemplateOptions CreateDigestTemplateOptions()
    {
        var options = new TemplateOptions();
        options.MemberAccessStrategy.Register<DigestTemplateModel>();
        return options;
    }

    /// <summary>
    /// The digest MJML template is kept as a standalone, designer-editable file
    /// (Templates/NotificationDigest.mjml.liquid) rather than a C# string, and embedded into the
    /// assembly so it ships without relying on the output directory layout.
    /// </summary>
    private static IFluidTemplate LoadDigestTemplate()
    {
        var resourceName =
            $"{typeof(NotificationSenderService).Namespace}.Templates.NotificationDigest.mjml.liquid";
        using var stream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded MJML template '{resourceName}' was not found."
            );
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();

        var parser = new FluidParser();
        if (!parser.TryParse(source, out var template, out var error))
        {
            throw new InvalidOperationException(
                $"Failed to parse MJML template '{resourceName}': {error}"
            );
        }

        return template;
    }

    private sealed record DigestTemplateModel(
        int NumNewSubjects,
        string StudyAcronym,
        string ScreeningListLink
    );
}
