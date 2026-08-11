namespace list.Models;

/// <summary>One eligibility criterion's assessment status for a specific patient within a trial.</summary>
public sealed class CriterionStatusDto
{
    public required string DisplayText { get; init; }

    /// <summary>true = met, false = not met, null = unknown (Observation.dataAbsentReason).</summary>
    public bool? Met { get; init; }
}
