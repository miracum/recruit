namespace RecruIT.List.Models;

/// <summary>
/// A trial's stable business identity: the (system, value) pair from ResearchStudy.identifier -
/// the same key the query module's producer itself uses to find/reuse a ResearchStudy across
/// runs (ResearchStudy?identifier={system}|{value}). Deliberately not the FHIR logical id
/// (server-local, not portable) and not the acronym (free-text display, renamable, not
/// guaranteed unique) - see the trial access authorization plan for why.
/// </summary>
public sealed record TrialIdentifier(string System, string Value)
{
    /// <summary>
    /// The "{System}|{Value}" form used as an exact-match key everywhere a trial identifier is
    /// stored as a plain string (NotificationSubscription/NotificationEvent.TrialIdentifier,
    /// ScreeningNote.TrialIdentifier). Centralized here so every writer/reader agrees on the exact
    /// same format.
    /// </summary>
    public string ToToken() => $"{System}|{Value}";
}
