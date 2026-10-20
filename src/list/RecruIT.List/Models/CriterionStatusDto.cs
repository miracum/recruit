namespace RecruIT.List.Models;

/// <summary>One eligibility criterion's assessment status for a specific patient within a trial.</summary>
public sealed class CriterionStatusDto
{
    public required string DisplayText { get; init; }

    /// <summary>true = met, false = not met, null = unresolved (see Indeterminate for why).</summary>
    public bool? Met { get; init; }

    /// <summary>
    /// Only meaningful when Met is null: false means the underlying data was simply missing
    /// (unknown), true means the criterion was evaluated but the result was inconclusive. Both
    /// count identically as "unresolved" - this only changes how the result is displayed.
    /// </summary>
    public bool Indeterminate { get; init; }

    /// <summary>Optional free-text explanation from the criterion's SQL (its result_note column).</summary>
    public string? Note { get; init; }
}
