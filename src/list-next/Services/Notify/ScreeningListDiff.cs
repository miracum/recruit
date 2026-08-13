using FhirList = Hl7.Fhir.Model.List;

namespace list.Services.Notify;

/// <summary>
/// Pure diffing logic, deliberately free of any FHIR-client or DB dependency so it's trivially
/// unit-testable: given the `List` snapshot a cursor last saw and the current snapshot, which
/// entries (by reference, e.g. "Patient/123") are genuinely new?
/// </summary>
public static class ScreeningListDiff
{
    public static IReadOnlyList<string> NewEntryReferences(FhirList? previous, FhirList current)
    {
        var previousRefs = EntryReferences(previous);
        return EntryReferences(current).Where(reference => !previousRefs.Contains(reference)).ToList();
    }

    private static HashSet<string> EntryReferences(FhirList? list)
    {
        var refs = new HashSet<string>();
        if (list is null)
        {
            return refs;
        }
        foreach (var entry in list.Entry)
        {
            if (entry.Item?.Reference is { Length: > 0 } reference)
            {
                refs.Add(reference);
            }
        }
        return refs;
    }
}
