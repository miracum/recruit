namespace list.Resources;

/// <summary>Maps a FHIR ResearchSubject.status literal to the resx key for its localized display label.</summary>
public static class StatusDisplay
{
    public static string GetKey(string fhirStatus) =>
        fhirStatus switch
        {
            "candidate" => "App.Status.Candidate",
            "screening" => "App.Status.Screening",
            "eligible" => "App.Status.Eligible",
            "ineligible" => "App.Status.Ineligible",
            "on-study" => "App.Status.OnStudy",
            "withdrawn" => "App.Status.Withdrawn",
            _ => fhirStatus,
        };
}
