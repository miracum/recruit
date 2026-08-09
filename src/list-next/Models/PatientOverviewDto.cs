namespace list.Models;

/// <summary>A single patient's demographics plus every trial they've been recommended for, across all trials the current user can access.</summary>
public sealed class PatientOverviewDto
{
    public required string PatientId { get; init; }

    public string? Name { get; init; }

    public DateTimeOffset? BirthDate { get; init; }

    public int? Age => BirthDate is null ? null : AgeCalculator.Calculate(BirthDate.Value);

    public string? Gender { get; init; }

    /// <summary>One entry per trial the patient is recommended for; StudyAcronym/ListId are populated on each.</summary>
    public required List<PatientListEntryDto> Trials { get; init; }
}
