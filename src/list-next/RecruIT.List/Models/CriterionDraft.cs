namespace RecruIT.List.Models;

/// <summary>
/// In-memory draft of a single eligibility criterion: one Group.characteristic backed either by a
/// newly-authored Library (Sql populated) or a reference to an existing, already-persisted one
/// (ExistingLibraryReference populated) - see docs/trino/eligibility-criteria-design.md.
/// </summary>
public sealed class CriterionDraft
{
    /// <summary>Local-only identity for editing/removing this row in the UI, never sent anywhere.</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    public string DisplayText { get; set; } = string.Empty;

    /// <summary>Mirrors Group.characteristic.exclude: true = exclusion criterion.</summary>
    public bool Exclude { get; set; }

    /// <summary>When set (e.g. "Library/common-age-min-18"), reuses an existing Library instead of authoring a new one.</summary>
    public string ExistingLibraryReference { get; set; } = string.Empty;

    public string Sql { get; set; } = string.Empty;
}
