namespace list.Models;

/// <summary>
/// One screening note for a ResearchSubject, stored in the screening_notes Postgres table (see
/// ScreeningNoteService). Multiple of these can exist per subject - each save inserts a new,
/// immutable row.
/// </summary>
public sealed class ScreeningNoteDto
{
    public required string Text { get; init; }

    public string? Author { get; init; }

    public DateTimeOffset? Time { get; init; }
}
