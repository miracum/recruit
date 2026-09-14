using Ganss.Xss;

namespace RecruIT.List.Services;

/// <summary>
/// Single shared Ganss.Xss sanitizer config for screening-note HTML - used both when persisting a
/// note (ScreeningNoteService.AddNoteAsync, the actual storage invariant) and when rendering one
/// (PatientNotesSection/PatientHistorySection, defense-in-depth), so the two can't drift apart.
/// </summary>
public static class NoteHtmlSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = new();

    public static string Sanitize(string html) => Sanitizer.Sanitize(html);
}
