using RecruIT.FhirConstants;

namespace RecruIT.List.Services.Fhir;

public static class FhirConstants
{
    public static string SystemScreeningList => Recruit.CodeSystems.Urls.ScreeningListType;
    public static string ScreeningListCode =>
        Recruit.CodeSystems.ScreeningListType.ScreeningRecommendations.Code();

    public static string UrlListBelongsToStudy => Recruit.Extensions.Urls.BelongsToStudy;

    public static string UrlResearchSubjectIdentifier =>
        Recruit.NamingSystems.ResearchSubjectId.Uri;

    /// <summary>
    /// The identifier system for an eligibility Group's business identifier, which is the study's
    /// own identifier value - used as the conditional-update key for the study's Group, same
    /// convention as ScreeningListId for the study's List.
    /// </summary>
    public static string UrlEligibilityGroupIdentifier =>
        Recruit.NamingSystems.EligibilityGroupId.Uri;

    /// <summary>The identifier system for a criterion Library authored directly through this UI.</summary>
    public static string UrlUiCreatedEligibilityLibraryIdentifier =>
        Recruit.NamingSystems.UiCreatedEligibilityLibraryId.Uri;

    /// <summary>
    /// The identifier system for a ResearchStudy authored directly through this UI - independent of
    /// whichever "real" business identifier (see TrialIdentifier) the admin may separately enter;
    /// gives a study a stable, title-derived identity to key update-as-create off of even when no
    /// such business identifier exists yet.
    /// </summary>
    public static string UrlUiCreatedResearchStudyIdentifier =>
        Recruit.NamingSystems.UiCreatedResearchStudyId.Uri;

    /// <summary>
    /// The identifier system for a screening List's business identifier - same system
    /// query-sql-on-fhir's EligibilityBundleBuilder keys its own List updates off of, so a List
    /// pre-created through this UI is the one a later successful poll updates in place rather than
    /// duplicates.
    /// </summary>
    public static string UrlScreeningListIdentifier => Recruit.NamingSystems.ScreeningListId.Uri;

    public const string SystemIdentifierType = "http://terminology.hl7.org/CodeSystem/v2-0203";
    public const string IdentifierTypeMedicalRecordNumber = "MR";

    public const string SystemDeterminedSubjectStatus =
        "https://fhir.miracum.org/uc1/CodeSystem/system-determined-subject-status";
    public const string DeterminedStatusIneligible = "ineligible";

    public static string SystemObservationCategory =>
        Recruit.CodeSystems.Urls.EligibilityAssessmentCategory;
    public static string ObservationCategoryEligibilityAssessment =>
        Recruit.CodeSystems.EligibilityAssessmentCategory.EligibilityAssessment.Code();

    public const string SnomedCodeYes = "373066001";
    public const string SnomedCodeNo = "373067005";
    public const string SnomedCodeIndeterminate = "82334004";

    public const string ListStatusCurrent = "current";

    public static readonly string[] RecruitedStatuses = ["on-study"];
    public static readonly string[] NotRecruitedStatuses = ["ineligible", "withdrawn"];
}
