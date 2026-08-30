namespace RecruIT.List.Models;

/// <summary>
/// One step of a study's eligibility attrition funnel (see EligibilityFunnelService): either the
/// total screened population, or the population remaining after cumulatively applying one more
/// criterion, in the same order as the study's Group.characteristic.
/// </summary>
public sealed class FunnelStepDto
{
    /// <summary>The criterion's display text - null when IsTotalPopulation, which has no single criterion to name (the caller supplies its own localized label).</summary>
    public string? Label { get; init; }

    public required int Count { get; init; }

    public required bool IsTotalPopulation { get; init; }
}
