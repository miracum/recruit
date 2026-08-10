namespace list.Services.Fhir;

/// <summary>
/// FHIR coding systems and extension URLs used by the `query` module (the producer of the data
/// this app reads) and by this app itself (for status/note writes). Verified against
/// src/query's FhirCohortTransactionBuilder.java, not just the previous frontend.
/// </summary>
public static class FhirConstants
{
    public const string SystemScreeningList = "https://fhir.miracum.org/uc1/CodeSystem/screeningList";
    public const string ScreeningListCode = "screening-recommendations";

    public const string UrlListBelongsToStudy = "https://fhir.miracum.org/uc1/StructureDefinition/belongsToStudy";
    public const string UrlStudyAcronym = "https://fhir.miracum.org/uc1/StructureDefinition/studyAcronym";

    /// <summary>Written by this app (list), not by the query module.</summary>
    public const string UrlResearchSubjectNote = "https://fhir.miracum.org/uc1/StructureDefinition/researchSubjectNote";

    public const string SystemIdentifierType = "http://terminology.hl7.org/CodeSystem/v2-0203";
    public const string IdentifierTypeMedicalRecordNumber = "MR";

    public const string SystemDeterminedSubjectStatus = "https://fhir.miracum.org/uc1/CodeSystem/system-determined-subject-status";
    public const string DeterminedStatusIneligible = "ineligible";

    /// <summary>Matches query-fhir-trino's fhir.systems.eligibility-criteria-types config default.</summary>
    public const string SystemEligibilityCriteriaTypes = "https://miracum.github.io/recruit/fhir/CodeSystem/eligibility-criteria-types";
    public const string EligibilityCriteriaTypeTrinoSql = "trino-sql";

    public const string ListStatusCurrent = "current";
    public const string ListStatusRetired = "retired";

    public static readonly string[] RecruitedStatuses = ["on-study"];
    public static readonly string[] PendingStatuses = ["candidate", "screening", "eligible"];
    public static readonly string[] NotRecruitedStatuses = ["ineligible", "withdrawn"];
}
