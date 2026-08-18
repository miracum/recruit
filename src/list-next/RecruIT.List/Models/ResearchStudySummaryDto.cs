namespace RecruIT.List.Models;

/// <summary>Read-only summary of an existing ResearchStudy, for the "reuse an existing study" picker.</summary>
public sealed record ResearchStudySummaryDto(
    string Id,
    string? Title,
    string? Acronym,
    string? Description,
    TrialIdentifier? TrialIdentifier
);
