namespace RecruIT.List.Models;

/// <summary>
/// In-memory draft of a ResearchStudy being authored on the criteria builder page. Either
/// references an existing, already-persisted study (ExistingId set) or describes a new one to be
/// created alongside its criteria.
/// </summary>
public sealed class ResearchStudyDraft
{
    public string? ExistingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Acronym { get; set; } = string.Empty;
    public string IdentifierSystem { get; set; } = string.Empty;
    public string IdentifierValue { get; set; } = string.Empty;
}
