using Hl7.Fhir.Model;
using RecruIT.List.Services.Notify;
using FhirList = Hl7.Fhir.Model.List;

namespace RecruIT.List.Tests;

public sealed class ScreeningListDiffTests
{
    [Fact]
    public void NewEntryReferences_ReturnsOnlyReferencesNotInPrevious()
    {
        var previous = ListOf("Patient/1", "Patient/2");
        var current = ListOf("Patient/1", "Patient/2", "Patient/3");

        var result = ScreeningListDiff.NewEntryReferences(previous, current);

        Assert.Equal(["Patient/3"], result);
    }

    [Fact]
    public void NewEntryReferences_ReturnsEmpty_WhenNothingChanged()
    {
        var previous = ListOf("Patient/1");
        var current = ListOf("Patient/1");

        Assert.Empty(ScreeningListDiff.NewEntryReferences(previous, current));
    }

    [Fact]
    public void NewEntryReferences_TreatsRemovedThenReAddedPatientAsNew()
    {
        // Patient/1 removed, Patient/2 added - because there's no permanent per-patient
        // notified-history, a re-added patient is (deliberately) treated as new again.
        var previous = ListOf("Patient/1");
        var current = ListOf("Patient/2");

        Assert.Equal(["Patient/2"], ScreeningListDiff.NewEntryReferences(previous, current));
    }

    [Fact]
    public void NewEntryReferences_HandlesNullPrevious()
    {
        var current = ListOf("Patient/1");

        Assert.Equal(["Patient/1"], ScreeningListDiff.NewEntryReferences(null, current));
    }

    private static FhirList ListOf(params string[] references)
    {
        var list = new FhirList();
        foreach (var reference in references)
        {
            list.Entry.Add(new FhirList.EntryComponent { Item = new ResourceReference(reference) });
        }
        return list;
    }
}
