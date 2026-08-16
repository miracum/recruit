using Recruit;

namespace list.Services.Fhir;

public static class FhirConstants
{
    public static string SystemScreeningList => Recruit.Recruit.CodeSystems.Urls.ScreeningListType;
    public static string ScreeningListCode =>
        Recruit.Recruit.CodeSystems.ScreeningListType.ScreeningRecommendations.Code();

    public static string UrlListBelongsToStudy =>
        Recruit.Recruit.Extensions.Urls.ScreeningListBelongsToStudy;

    public static string UrlResearchSubjectIdentifier =>
        Recruit.Recruit.NamingSystems.ResearchSubjectId.UniqueId.Uri;

    public const string SystemIdentifierType = "http://terminology.hl7.org/CodeSystem/v2-0203";
    public const string IdentifierTypeMedicalRecordNumber = "MR";

    public const string SystemDeterminedSubjectStatus =
        "https://fhir.miracum.org/uc1/CodeSystem/system-determined-subject-status";
    public const string DeterminedStatusIneligible = "ineligible";

    public static string SystemObservationCategory =>
        Recruit.Recruit.CodeSystems.Urls.EligibilityAssessmentCategory;
    public static string ObservationCategoryEligibilityAssessment =>
        Recruit.Recruit.CodeSystems.EligibilityAssessmentCategory.EligibilityAssessment.Code();

    public const string SystemSnomed = "http://snomed.info/sct";

    public const string SnomedCodeYes = "373066001";
    public const string SnomedCodeNo = "373067005";
    public const string SnomedCodeUnknown = "261665006";
    public const string SnomedCodeIndeterminate = "82334004";

    public const string ListStatusCurrent = "current";
    public const string ListStatusRetired = "retired";

    public static readonly string[] RecruitedStatuses = ["on-study"];
    public static readonly string[] PendingStatuses = ["candidate", "screening", "eligible"];
    public static readonly string[] NotRecruitedStatuses = ["ineligible", "withdrawn"];
}
