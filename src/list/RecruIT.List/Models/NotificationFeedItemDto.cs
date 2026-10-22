namespace RecruIT.List.Models;

/// <summary>
/// One in-app NotificationDelivery, joined with its NotificationEvent for display. ReadAt is null
/// for unread items.
/// TrialIdentifier is the raw "system|value" token (see TrialIdentifier) - resolving it to a
/// current List id/acronym for display is the caller's job (see
/// ScreeningListService.ResolveListForTrialAsync), not done here since this type only ever comes
/// from a plain DB read.
/// </summary>
public sealed record NotificationFeedItemDto(
    Guid DeliveryId,
    string TrialIdentifier,
    string PatientDisplayName,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ReadAt
);
