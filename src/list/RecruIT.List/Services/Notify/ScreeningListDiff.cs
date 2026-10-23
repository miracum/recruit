using FhirList = Hl7.Fhir.Model.List;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// Pure diffing logic, deliberately free of any FHIR-client or DB dependency so it's trivially
/// unit-testable: given every `List` version since the one a cursor last saw (that one included)
/// and the current one, which entries (by reference, e.g. "ResearchSubject/123") are genuinely
/// new?
/// </summary>
public static class ScreeningListDiff
{
    /// <summary>
    /// The current entries missing from at least one of earlierVersions - not just from the
    /// oldest one, so a patient removed and re-added between two polls counts as new again, the
    /// same as if a poll had seen the version without them. A null version (a deleted one, from
    /// the instance history) has no entries. Patients added and removed again in between aren't
    /// in current, so they're never reported.
    /// </summary>
    public static IReadOnlyList<string> NewEntryReferences(
        IEnumerable<FhirList?> earlierVersions,
        FhirList current
    )
    {
        var earlierRefs = earlierVersions.Select(EntryReferences).ToList();
        return EntryReferences(current)
            .Where(reference => earlierRefs.Any(refs => !refs.Contains(reference)))
            .ToList();
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
