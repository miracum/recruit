namespace Recruit.List.Web.Fhir;

public interface IFhirService
{
    /// <summary>Fetches all ResearchSubject resources (with their referenced Patient and ResearchStudy) from the FHIR server.</summary>
    Task<IReadOnlyList<ResearchSubjectRow>> GetResearchSubjectsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>Updates a ResearchSubject's recruitment status and/or note via a FHIR PATCH request.</summary>
    Task UpdateResearchSubjectAsync(
        string researchSubjectId,
        string status,
        string? note,
        CancellationToken cancellationToken = default
    );
}
