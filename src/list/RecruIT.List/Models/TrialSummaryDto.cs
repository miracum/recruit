namespace RecruIT.List.Models;

public sealed class TrialSummaryDto
{
    public required string ListId { get; init; }

    /// <summary>The ResearchStudy's own FHIR logical id - e.g. to look up its attrition funnel via EligibilityFunnelService.</summary>
    public required string StudyId { get; init; }

    /// <summary>The trial's stable business identity - see TrialIdentifier. Drives all access checks.</summary>
    public required TrialIdentifier TrialIdentifier { get; init; }

    public required string StudyAcronym { get; init; }

    public string? StudyTitle { get; init; }

    public string? StudyDescription { get; init; }

    /// <summary>The study's eligibility criteria (ResearchStudy.enrollment -> Group.characteristic), in Group.characteristic order.</summary>
    public IReadOnlyList<CriterionDefinitionDto> Criteria { get; init; } = [];

    /// <summary>FHIR List.status: "current" or "retired".</summary>
    public required string ListStatus { get; init; }

    public int RecruitedCount { get; init; }

    public int PendingCount { get; init; }

    public int NotRecruitedCount { get; init; }

    public int TotalCount => RecruitedCount + PendingCount + NotRecruitedCount;
}
