namespace list.Data.Entities;

/// <summary>
/// One screening note added to a ResearchSubject. Rows are immutable/insert-only - never updated
/// or deleted - preserving the append-only audit trail the old FHIR Annotation-extension design
/// had, without needing FHIR version history for it. Keyed by business identifiers ("system|value",
/// see FhirConstants.UrlResearchSubjectIdentifier / TrialIdentifier), not FHIR logical ids.
/// </summary>
public sealed class ScreeningNote
{
    public Guid Id { get; set; }

    public required string ResearchSubjectIdentifier { get; set; }

    public required string TrialIdentifier { get; set; }

    public required string Text { get; set; }

    /// <summary>OIDC "sub" of the author, if known.</summary>
    public string? AuthorSubjectId { get; set; }

    /// <summary>Snapshot of the author's display name at write time.</summary>
    public required string AuthorDisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
