namespace list.Models;

/// <summary>
/// One entry from ResearchSubject's repeating researchSubjectNote extension (FHIR Annotation:
/// author + time + text). Multiple of these can exist on a single ResearchSubject - each save
/// appends one, it never overwrites previous notes.
/// </summary>
public sealed class PatientNoteDto
{
    public required string Text { get; init; }

    public string? Author { get; init; }

    public DateTimeOffset? Time { get; init; }
}
