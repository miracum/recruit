namespace list.Models;

public enum NotificationKind
{
    NewRecommendation,
    StatusChanged,
}

public sealed class NotificationEventDto
{
    /// <summary>Deterministic key (e.g. "new:{researchSubjectId}") used for dismissal tracking.</summary>
    public required string Id { get; init; }

    public required NotificationKind Kind { get; init; }

    public required string ListId { get; init; }

    /// <summary>The trial's stable business identity - see TrialIdentifier.</summary>
    public required TrialIdentifier TrialIdentifier { get; init; }

    public required string StudyAcronym { get; init; }

    public string? PatientId { get; init; }

    public string? PatientName { get; init; }

    public required string Message { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
