using RecruIT.List.Models;

namespace RecruIT.List.Data.Entities;

/// <summary>
/// One NotificationEvent, materialized for one NotificationSubscription, on one channel - the row
/// that actually drives sending/reading. SubjectId/Email are denormalized from the subscription so
/// the bell's hot "unread count" query needs no join. InApp rows are always created immediately
/// (ScheduledFor = event.OccurredAt, no batching concept) and never have SentAt set; Email rows are
/// only created for Frequency == Instant in this milestone and are picked up by
/// NotificationSenderService once ScheduledFor has passed.
/// </summary>
public sealed class NotificationDelivery
{
    public Guid Id { get; set; }

    public Guid NotificationEventId { get; set; }

    public Guid NotificationSubscriptionId { get; set; }

    public required string SubjectId { get; set; }

    public required string Email { get; set; }

    public required NotificationChannel Channel { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    /// <summary>Email only - when NotificationSenderService actually sent this delivery.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>InApp only - when the subscriber dismissed/read this delivery in the feed.</summary>
    public DateTimeOffset? ReadAt { get; set; }
}
