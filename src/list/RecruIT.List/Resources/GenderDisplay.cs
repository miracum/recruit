namespace RecruIT.List.Resources;

/// <summary>Maps a FHIR AdministrativeGender literal to the resx key for its localized display label.</summary>
public static class GenderDisplay
{
    public static string GetKey(string fhirGender) =>
        fhirGender switch
        {
            "male" => "App.Gender.Male",
            "female" => "App.Gender.Female",
            "other" => "App.Gender.Other",
            "unknown" => "App.Gender.Unknown",
            _ => fhirGender,
        };
}
