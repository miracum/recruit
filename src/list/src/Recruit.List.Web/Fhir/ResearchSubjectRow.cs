namespace Recruit.List.Web.Fhir;

/// <summary>
/// A single row of the patient/ResearchSubject grid, joining a FHIR ResearchSubject
/// with its referenced Patient (and, if available, ResearchStudy) resources.
/// </summary>
public sealed class ResearchSubjectRow
{
    public required string ResearchSubjectId { get; init; }
    public required string PatientId { get; init; }
    public string Status { get; set; } = "candidate";
    public string? Note { get; set; }
    public string? MrNumber { get; init; }
    public int? BirthYear { get; init; }
    public string? Gender { get; init; }
    public string? StudyDisplay { get; init; }
    public DateTimeOffset? EnrolledSince { get; init; }

    /// <summary>The patient identifier shown in the grid: the MR number if known, the raw Patient id otherwise.</summary>
    public string DisplayId => MrNumber ?? PatientId;

    public string DemographicsDisplay =>
        (BirthYear?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown")
        + ", "
        + (Gender ?? "unknown");
}
