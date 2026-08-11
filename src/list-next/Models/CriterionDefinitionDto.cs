namespace list.Models;

/// <summary>One eligibility criterion's definition (not a per-patient status) - Group.characteristic.</summary>
public sealed class CriterionDefinitionDto
{
    public required string DisplayText { get; init; }

    public bool Exclude { get; init; }

    public string? Sql { get; init; }
}
