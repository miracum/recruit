using RecruIT.List.Models;

namespace RecruIT.List.Data.Entities;

/// <summary>
/// One user's notification preference for one trial - self-service only (created by the
/// subscriber themselves while authenticated), so unlike TrialAccessGrant there's no
/// email-invite/backfill case: SubjectId is always known at creation time. Row presence means
/// subscribed; unsubscribing deletes the row. Keyed by the trial's business identifier formatted
/// as "system|value", same convention as ScreeningNote.TrialIdentifier.
/// </summary>
public sealed class NotificationSubscription
{
    public Guid Id { get; set; }

    public required string TrialIdentifier { get; set; }

    /// <summary>OIDC "sub" of the subscriber.</summary>
    public required string SubjectId { get; set; }

    public required string Email { get; set; }

    public required NotificationFrequency Frequency { get; set; }

    /// <summary>Only meaningful when Frequency is Weekly.</summary>
    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>Only meaningful when Frequency is Daily or Weekly - Monthly is always the 1st, no configurable time yet.</summary>
    public TimeOnly? TimeOfDay { get; set; }

    /// <summary>IANA time zone captured from the browser at subscribe time, used to evaluate DayOfWeek/TimeOfDay in the subscriber's own local time.</summary>
    public required string TimeZoneId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
