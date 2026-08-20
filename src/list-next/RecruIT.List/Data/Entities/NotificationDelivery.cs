using RecruIT.List.Models;

namespace RecruIT.List.Data.Entities;

/// <summary>
/// One NotificationEvent, materialized for one NotificationSubscription, on one channel - the row
/// that actually drives sending/reading. SubjectId/Email are denormalized from the subscription so
/// the bell's hot "unread count" query needs no join. Both InApp and Email rows are created
/// immediately for every subscription when the event is detected (ScheduledFor = event.OccurredAt);
/// InApp rows never get SentAt set (only ReadAt, once dismissed). Email rows are picked up by
/// NotificationSenderService, which decides at *send* time - not from ScheduledFor - whether the
/// owning subscription is actually due yet, based on its current Frequency/DayOfWeek/TimeOfDay
/// against the last time it was successfully sent. That's deliberate: it's what makes a
/// subscriber's schedule change take effect on the very next tick, including for deliveries
/// already queued before the change - see NotificationSenderService.SendDueAsync.
/// </summary>
public sealed class NotificationDelivery
{
    public Guid Id { get; set; }

    public Guid NotificationEventId { get; set; }

    public Guid NotificationSubscriptionId { get; set; }

    public required string SubjectId { get; set; }

    public required string Email { get; set; }

    public required NotificationChannel Channel { get; set; }

    /// <summary>
    /// When this row was queued (the owning NotificationEvent's OccurredAt) - a record of intent,
    /// not a scheduling input: NotificationSenderService recomputes due-ness itself (see class
    /// summary) rather than reading this back.
    /// </summary>
    public DateTimeOffset ScheduledFor { get; set; }

    /// <summary>Email only - when NotificationSenderService actually sent this delivery.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>InApp only - when the subscriber dismissed/read this delivery in the feed.</summary>
    public DateTimeOffset? ReadAt { get; set; }
}
