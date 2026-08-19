namespace RecruIT.List.Models;

/// <summary>
/// In-memory draft of a new ResearchStudy being authored on the criteria builder page. Editing an
/// existing ResearchStudy isn't supported yet - every draft describes one to be created (or
/// replaced, via update-as-create keyed off its title-derived identifier) alongside its criteria.
/// </summary>
public sealed class ResearchStudyDraft
{
    public string Title { get; set; } = string.Empty;
    public string Acronym { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
