namespace list.Models;

public sealed class TrialSummaryDto
{
    public required string ListId { get; init; }

    public required string StudyAcronym { get; init; }

    public string? StudyTitle { get; init; }

    public string? StudyDescription { get; init; }

    /// <summary>FHIR List.status: "current" or "retired".</summary>
    public required string ListStatus { get; init; }

    public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>Set when List.note indicates the cohort was truncated at a size threshold.</summary>
    public string? TruncationNote { get; init; }

    public int RecruitedCount { get; init; }

    public int PendingCount { get; init; }

    public int NotRecruitedCount { get; init; }

    public int TotalCount => RecruitedCount + PendingCount + NotRecruitedCount;

    public int NewSuggestionsCount { get; init; }

    /// <summary>Pending patients (candidate/screening/eligible) not touched within the configured window.</summary>
    public int StalledLeadsCount { get; init; }
}
