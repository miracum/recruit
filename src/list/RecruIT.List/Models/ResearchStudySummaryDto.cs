namespace RecruIT.List.Models;

/// <summary>Read-only summary of an existing ResearchStudy resource on the FHIR server, for the "use an existing study" picker.</summary>
public sealed record ResearchStudySummaryDto(
    string Id,
    string? Title,
    string? Acronym,
    string? Description,
    string? Status
);
