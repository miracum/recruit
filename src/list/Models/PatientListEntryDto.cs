namespace list.Models;

public sealed class PatientListEntryDto
{
    public required string ResearchSubjectId { get; init; }

    public required string PatientId { get; init; }

    public string? Name { get; init; }

    public DateTimeOffset? BirthDate { get; init; }

    public int? Age => BirthDate is null ? null : CalculateAge(BirthDate.Value);

    public string? Gender { get; init; }

    /// <summary>ResearchSubject.status: candidate, screening, eligible, ineligible, on-study, withdrawn.</summary>
    public required string Status { get; set; }

    public string? Note { get; set; }

    /// <summary>List.entry.date - when the patient was first recommended for this trial.</summary>
    public DateTimeOffset? RecommendedDate { get; init; }

    /// <summary>Set when List.entry.flag indicates the query module determined the patient is no longer eligible.</summary>
    public bool SystemDeterminedIneligible { get; init; }

    /// <summary>Populated lazily/on-demand from the patient's most recent Encounter.</summary>
    public string? LastKnownLocation { get; set; }

    public bool LastKnownLocationLoaded { get; set; }

    public bool IsNew(int windowDays) =>
        RecommendedDate is not null && RecommendedDate.Value >= DateTimeOffset.UtcNow.AddDays(-windowDays);

    private static int CalculateAge(DateTimeOffset birthDate)
    {
        var today = DateTimeOffset.UtcNow;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age).Date)
        {
            age--;
        }

        return age;
    }
}
