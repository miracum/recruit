using RecruIT.List.Models;

namespace RecruIT.List.Data.Entities;

/// <summary>
/// One recipient's subscription to notifications for one trial, via one channel. Keyed by the
/// trial's ResearchStudy business identifier, same as TrialAccessGrant - see TrialIdentifier.
/// </summary>
public sealed class NotificationRecipient
{
    public Guid Id { get; set; }

    public required string TrialIdentifierSystem { get; set; }

    public required string TrialIdentifierValue { get; set; }

    public required string Email { get; set; }

    public required NotificationChannel Channel { get; set; }

    /// <summary>Sub or email of whoever created this recipient, for basic traceability.</summary>
    public required string CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
