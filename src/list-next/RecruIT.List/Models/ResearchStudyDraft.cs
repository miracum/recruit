namespace RecruIT.List.Models;

/// <summary>
/// In-memory draft of the ResearchStudy being authored on the criteria builder page, either a new
/// one (created, or replaced via update-as-create keyed off its title-derived identifier) or an
/// existing one picked from the FHIR server (included in the bundle unmodified, aside from adding a
/// reference to the criteria Group) - see EligibilityCriteriaService.BuildPreviewBundle. Title,
/// Acronym and Description are always populated for display, but only feed the generated bundle
/// when ExistingStudyId is unset.
/// </summary>
public sealed class ResearchStudyDraft
{
    public string Title { get; set; } = string.Empty;
    public string Acronym { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>When set, the id of the existing FHIR ResearchStudy this draft's criteria are being authored for.</summary>
    public string? ExistingStudyId { get; set; }
}
