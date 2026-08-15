using Recruit;

namespace list.Services.Fhir;

/// <summary>
/// FHIR coding systems and extension URLs used by the `query` module (the producer of the data
/// this app reads) and by this app itself (for status/note writes). Verified against
/// src/query's FhirCohortTransactionBuilder.java, not just the previous frontend.
///
/// The constants below that come from recruit's own fhir/ig are sourced from the generated
/// Recruit class (see ../../fhir-constants-cs) rather than duplicated as literals - keeps this
/// file the single place call sites reference, while staying in sync with the IG. See
/// ../../fhir-constants-cs/README.md to regenerate after an IG change.
/// </summary>
public static class FhirConstants
{
    public static string SystemScreeningList => Recruit.Recruit.CodeSystems.Urls.ScreeningListType;
    public static string ScreeningListCode =>
        Recruit.Recruit.CodeSystems.ScreeningListType.ScreeningRecommendations.Code();

    public static string UrlListBelongsToStudy =>
        Recruit.Recruit.Extensions.Urls.ScreeningListBelongsToStudy;

    /// <summary>
    /// ResearchSubject's stable business identifier, set unconditionally by query-sql-on-fhir's
    /// EligibilityBundleBuilder (see fhir.systems.research-subject-identifier). Notes are keyed on
    /// this "system|value" pair rather than the FHIR logical id - see ScreeningNoteService.
    /// </summary>
    public static string UrlResearchSubjectIdentifier =>
        Recruit.Recruit.NamingSystems.ResearchSubjectId.UniqueId.Uri;

    public const string SystemIdentifierType = "http://terminology.hl7.org/CodeSystem/v2-0203";
    public const string IdentifierTypeMedicalRecordNumber = "MR";

    public const string SystemDeterminedSubjectStatus =
        "https://fhir.miracum.org/uc1/CodeSystem/system-determined-subject-status";
    public const string DeterminedStatusIneligible = "ineligible";

    public static string SystemObservationCategory =>
        Recruit.Recruit.CodeSystems.Urls.EligibilityObservationCategory;
    public static string ObservationCategoryEligibilityAssessment =>
        Recruit.Recruit.CodeSystems.EligibilityObservationCategory.EligibilityAssessment.Code();

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
