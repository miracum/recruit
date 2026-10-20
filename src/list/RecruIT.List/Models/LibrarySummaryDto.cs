namespace RecruIT.List.Models;

/// <summary>
/// Read-only summary of an existing eligibility-criterion Library resource, for the Criteria
/// Manager's catalog view. UsedByStudyDisplayNames is derived by resolving which Group(s)
/// reference this Library via Group.characteristic, then which ResearchStudy(ies) enroll those
/// Group(s) - empty when the Library isn't currently referenced by any study.
/// </summary>
public sealed record LibrarySummaryDto(
    string Id,
    string? Title,
    string? Name,
    string? Description,
    string? Status,
    string? Sql,
    DateTimeOffset? LastUpdated,
    IReadOnlyList<string> UsedByStudyDisplayNames
);
