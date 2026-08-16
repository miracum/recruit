namespace RecruIT.List.Models;

/// <summary>
/// A trial's stable business identity: the (system, value) pair from ResearchStudy.identifier -
/// the same key the query module's producer itself uses to find/reuse a ResearchStudy across
/// runs (ResearchStudy?identifier={system}|{value}). Deliberately not the FHIR logical id
/// (server-local, not portable) and not the acronym (free-text display, renamable, not
/// guaranteed unique) - see the trial access authorization plan for why.
/// </summary>
public sealed record TrialIdentifier(string System, string Value);
