namespace list.Services.Fhir;

/// <summary>
/// FHIR coding systems and extension URLs used by the `query` module (the producer of the data
/// this app reads) and by this app itself (for status/note writes). Verified against
/// src/query's FhirCohortTransactionBuilder.java, not just the previous frontend.
/// </summary>
public static class FhirConstants
{
    /// <summary>Matches recruit's fhir/ig - see the fhir-constants module (Recruit.CodeSystems.screeningListType()).</summary>
    public const string SystemScreeningList =
        "https://miracum.github.io/recruit/fhir/CodeSystem/screening-list-type";
    public const string ScreeningListCode = "screening-recommendations";

    /// <summary>Matches recruit's fhir/ig - see the fhir-constants module (Recruit.Extensions.screeningListBelongsToStudy()).</summary>
    public const string UrlListBelongsToStudy =
        "https://miracum.github.io/recruit/fhir/StructureDefinition/screening-list-belongs-to-study";

    /// <summary>Written by this app (list), not by the query module.</summary>
    public const string UrlResearchSubjectNote =
        "https://fhir.miracum.org/uc1/StructureDefinition/researchSubjectNote";

    public const string SystemIdentifierType = "http://terminology.hl7.org/CodeSystem/v2-0203";
    public const string IdentifierTypeMedicalRecordNumber = "MR";

    public const string SystemDeterminedSubjectStatus =
        "https://fhir.miracum.org/uc1/CodeSystem/system-determined-subject-status";
    public const string DeterminedStatusIneligible = "ineligible";

    public const string SystemEligibilityCriteriaTypes =
        "https://miracum.github.io/recruit/fhir/CodeSystem/eligibility-criteria-types";
    public const string EligibilityCriteriaTypeTrinoSql = "trino-sql";

    public const string SystemObservationCategory =
        "https://miracum.github.io/recruit/fhir/CodeSystem/eligibility-observation-category";
    public const string ObservationCategoryEligibilityAssessment = "eligibility-assessment";

    /// <summary>
    /// SNOMED CT "Yes/No/Unknown/Indeterminate (qualifier value)" codes used for the eligibility
    /// Observation's valueCodeableConcept - see EligibilityBundleBuilder.java's buildResultValue.
    /// </summary>
    public const string SystemSnomed = "http://snomed.info/sct";

    public const string SnomedCodeYes = "373066001";
    public const string SnomedCodeNo = "373067005";
    public const string SnomedCodeUnknown = "261665006";
    public const string SnomedCodeIndeterminate = "82334004";

    /// <summary>
    /// New system, minted by this app: keys a signed-in user's Practitioner by their OIDC "sub"
    /// (stable regardless of email/name changes). notify currently keys its own Practitioner
    /// resources by email under a different system (fhir.systems.subscriber-id,
    /// "https://fhir.miracum.org/uc1/identifiers/notification-subscriber-id") - reconciling the two
    /// so a coordinator's list-next identity and notify's CommunicationRequest-recipient identity
    /// resolve to the same resource is a follow-up on the notify side, not done here.
    /// </summary>
    public const string SystemPractitionerOidcSubject =
        "https://miracum.github.io/recruit/fhir/identifiers/practitioner-oidc-subject";

    public const string ListStatusCurrent = "current";
    public const string ListStatusRetired = "retired";

    public static readonly string[] RecruitedStatuses = ["on-study"];
    public static readonly string[] PendingStatuses = ["candidate", "screening", "eligible"];
    public static readonly string[] NotRecruitedStatuses = ["ineligible", "withdrawn"];
}
