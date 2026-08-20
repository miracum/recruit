using System.Security.Claims;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Utility;
using Microsoft.Extensions.Localization;
using RecruIT.List.Models;
using RecruIT.List.Resources;
using RecruIT.List.Services.Access;
using FhirList = Hl7.Fhir.Model.List;
using Task = System.Threading.Tasks.Task;

namespace RecruIT.List.Services.Fhir;

public sealed class ScreeningListService(
    FhirClientFactory clientFactory,
    TrialAccessService accessService,
    IStringLocalizer<SharedResources> localizer,
    ILogger<ScreeningListService> logger
)
{
    public async Task<IReadOnlyList<TrialSummaryDto>> GetAccessibleTrialsAsync(
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        var query = BuildScreeningListsQuery("current,retired");

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(client, query, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch screening lists from the FHIR server");
            throw new FhirAccessException(localizer["App.Errors.TrialsLoadFailed"], ex);
        }

        var researchSubjectsById = resources
            .OfType<ResearchSubject>()
            .ToDictionary(rs => rs.Id!, rs => rs);
        var lists = resources.OfType<FhirList>().ToList();

        var trialInfoByListId = BuildTrialInfoByListId(resources, lists);
        var accessibleTrials = await accessService.GetAccessibleTrialIdentifiersAsync(user, ct);

        var summaries = new List<TrialSummaryDto>();
        foreach (
            var (list, trialInfo) in GetAccessibleLists(lists, trialInfoByListId, accessibleTrials)
        )
        {
            var (recruited, pending, notRecruited) = CountByStatus(
                (list.Entry ?? []).Select(entry =>
                {
                    var subjectId = entry.Item?.Reference?.Split('/').LastOrDefault();
                    var subject =
                        subjectId is not null
                        && researchSubjectsById.TryGetValue(subjectId, out var s)
                            ? s
                            : null;
                    return subject is not null ? EnumUtility.GetLiteral(subject.Status) : null;
                })
            );

            summaries.Add(
                new TrialSummaryDto
                {
                    ListId = list.Id!,
                    TrialIdentifier = trialInfo.Identifier,
                    StudyAcronym = trialInfo.Acronym!,
                    StudyTitle = trialInfo.Title,
                    ListStatus =
                        EnumUtility.GetLiteral(list.Status)
                        ?? RecruIT.List.Services.Fhir.FhirConstants.ListStatusCurrent,
                    RecruitedCount = recruited,
                    PendingCount = pending,
                    NotRecruitedCount = notRecruited,
                }
            );
        }

        return summaries.OrderBy(s => s.StudyAcronym, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Patient-centric view of the same data the dashboard uses: instead of one row per trial, this
    /// groups every accessible List's entries by patient, so a patient recommended for several
    /// trials shows up once with a membership entry per trial ("potentially fitting studies").
    /// </summary>
    public async Task<IReadOnlyList<PatientOverviewDto>> GetPatientsAcrossTrialsAsync(
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        var query = BuildScreeningListsQuery("current");

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(client, query, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch screening lists for the patient overview");
            throw new FhirAccessException(localizer["App.Errors.PatientOverviewLoadFailed"], ex);
        }

        var subjectsById = resources.OfType<ResearchSubject>().ToDictionary(rs => rs.Id!, rs => rs);
        var patientsById = resources.OfType<Patient>().ToDictionary(p => p.Id!, p => p);
        var lists = resources.OfType<FhirList>().ToList();

        var trialInfoByListId = BuildTrialInfoByListId(resources, lists);
        var accessibleTrials = await accessService.GetAccessibleTrialIdentifiersAsync(user, ct);

        var overviewsByPatient = new Dictionary<string, PatientOverviewDto>();

        foreach (
            var (list, trialInfo) in GetAccessibleLists(lists, trialInfoByListId, accessibleTrials)
        )
        {
            foreach (var entry in list.Entry ?? [])
            {
                var subjectId = entry.Item?.Reference?.Split('/').LastOrDefault();
                if (subjectId is null || !subjectsById.TryGetValue(subjectId, out var subject))
                {
                    continue;
                }

                var patientId = subject.Individual?.Reference?.Split('/').LastOrDefault();
                if (patientId is null)
                {
                    continue;
                }

                var patient = patientsById.TryGetValue(patientId, out var p) ? p : null;

                if (!overviewsByPatient.TryGetValue(patientId, out var overview))
                {
                    overview = new PatientOverviewDto
                    {
                        PatientId = patientId,
                        Name = FormatPatientName(patient),
                        BirthDate =
                            patient?.BirthDate is { } bd
                            && DateTimeOffset.TryParse(bd, out var birthDate)
                                ? birthDate
                                : null,
                        Gender = EnumUtility.GetLiteral(patient?.Gender),
                        Trials = [],
                    };
                    overviewsByPatient[patientId] = overview;
                }

                DateTimeOffset? recommendedDate =
                    entry.Date is { } d && DateTimeOffset.TryParse(d, out var parsed)
                        ? parsed
                        : null;
                var isFlaggedIneligible =
                    entry.Flag?.Coding?.Any(c =>
                        c.System
                            == RecruIT
                                .List
                                .Services
                                .Fhir
                                .FhirConstants
                                .SystemDeterminedSubjectStatus
                        && c.Code
                            == RecruIT.List.Services.Fhir.FhirConstants.DeterminedStatusIneligible
                    ) ?? false;

                overview.Trials.Add(
                    new PatientListEntryDto
                    {
                        ResearchSubjectId = subject.Id!,
                        ResearchSubjectIdentifier = subject.GetResearchSubjectIdentifierToken(),
                        PatientId = patientId,
                        Name = overview.Name,
                        BirthDate = overview.BirthDate,
                        Gender = overview.Gender,
                        Phone = patient
                            ?.Telecom?.FirstOrDefault(t =>
                                t.System == ContactPoint.ContactPointSystem.Phone
                            )
                            ?.Value,
                        Email = patient
                            ?.Telecom?.FirstOrDefault(t =>
                                t.System == ContactPoint.ContactPointSystem.Email
                            )
                            ?.Value,
                        Status = EnumUtility.GetLiteral(subject.Status) ?? "candidate",
                        RecommendedDate = recommendedDate,
                        SystemDeterminedIneligible = isFlaggedIneligible,
                        LastUpdated = subject.Meta?.LastUpdated,
                        ListId = list.Id,
                        StudyAcronym = trialInfo.Acronym,
                        StudyTitle = trialInfo.Title,
                        TrialIdentifier = trialInfo.Identifier,
                    }
                );
            }
        }

        foreach (var overview in overviewsByPatient.Values)
        {
            overview.Trials.Sort(
                (a, b) =>
                    string.Compare(
                        a.StudyAcronym,
                        b.StudyAcronym,
                        StringComparison.OrdinalIgnoreCase
                    )
            );
        }

        return overviewsByPatient
            .Values.OrderBy(o => o.Name ?? o.PatientId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<(
        TrialSummaryDto Summary,
        IReadOnlyList<PatientListEntryDto> Patients
    )> GetListWithPatientsAsync(string listId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();

        var query =
            $"List?_id={Uri.EscapeDataString(listId)}"
            + "&_include=List:item"
            + "&_include:iterate=ResearchSubject:patient"
            + "&_include=List:belongs-to-study"
            + "&_include:iterate=ResearchStudy:enrollment"
            + "&_include:iterate=Group:characteristic";

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(client, query, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch List/{ListId} from the FHIR server", listId);
            throw new FhirAccessException(localizer["App.Errors.PatientListLoadFailed"], ex);
        }

        var list =
            resources.OfType<FhirList>().FirstOrDefault()
            ?? throw new FhirAccessException(localizer["App.Errors.ListNotFound"]);

        // The acronym, trial identifier (see TrialIdentifier) and title/description all live on
        // the ResearchStudy itself - pulled in via the belongs-to-study SearchParameter (see
        // src/hack/fhir/search-parameters-transaction.json) rather than trusting a Reference.display
        // text cached on the List extension (see GetStudyAcronym).
        var study = resources.OfType<ResearchStudy>().FirstOrDefault();
        var acronym = study?.GetStudyAcronym();
        var trialIdentifier = study?.GetTrialIdentifier();
        if (
            string.IsNullOrEmpty(acronym)
            || trialIdentifier is null
            || !await accessService.CanAccessTrialAsync(user, trialIdentifier, ct)
        )
        {
            throw new UnauthorizedAccessException(localizer["App.Errors.NotAuthorizedTrial"]);
        }

        // ResearchStudy.enrollment -> Group.characteristic, pulled in via the
        // researchstudy-enrollment-reference SearchParameter (see
        // src/hack/fhir/search-parameters-transaction.json) rather than a separate fetch.
        var enrolledGroupId = study?.Enrollment?.FirstOrDefault()?.GetReferencedId();
        var group = enrolledGroupId is not null
            ? resources.OfType<Group>().FirstOrDefault(g => g.Id == enrolledGroupId)
            : null;
        var librariesById = resources
            .OfType<Library>()
            .Where(l => l.Id is not null)
            .ToDictionary(l => l.Id!, l => l);
        var criteria = (group?.Characteristic ?? [])
            .Where(c => !string.IsNullOrEmpty(c.Code?.Text))
            .Select(c => new CriterionDefinitionDto
            {
                DisplayText = c.Code!.Text!,
                Exclude = c.Exclude ?? false,
                Sql =
                    (c.Value as ResourceReference).GetReferencedId() is { } libraryId
                    && librariesById.TryGetValue(libraryId, out var library)
                        ? library.GetSqlText()
                        : null,
            })
            .ToList();

        var subjectsById = resources.OfType<ResearchSubject>().ToDictionary(rs => rs.Id!, rs => rs);
        var patientsById = resources.OfType<Patient>().ToDictionary(p => p.Id!, p => p);

        var patientEntries = new List<PatientListEntryDto>();

        foreach (var entry in list.Entry ?? [])
        {
            var subjectId = entry.Item?.Reference?.Split('/').LastOrDefault();
            if (subjectId is null || !subjectsById.TryGetValue(subjectId, out var subject))
            {
                continue;
            }

            var patientId = subject.Individual?.Reference?.Split('/').LastOrDefault();
            var patient =
                patientId is not null && patientsById.TryGetValue(patientId, out var p) ? p : null;

            var status = EnumUtility.GetLiteral(subject.Status) ?? "candidate";
            DateTimeOffset? recommendedDate =
                entry.Date is { } d && DateTimeOffset.TryParse(d, out var parsed) ? parsed : null;
            var isFlaggedIneligible =
                entry.Flag?.Coding?.Any(c =>
                    c.System
                        == RecruIT.List.Services.Fhir.FhirConstants.SystemDeterminedSubjectStatus
                    && c.Code == RecruIT.List.Services.Fhir.FhirConstants.DeterminedStatusIneligible
                ) ?? false;

            patientEntries.Add(
                new PatientListEntryDto
                {
                    ResearchSubjectId = subject.Id!,
                    ResearchSubjectIdentifier = subject.GetResearchSubjectIdentifierToken(),
                    PatientId = patientId ?? string.Empty,
                    Name = FormatPatientName(patient),
                    MedicalRecordNumber = patient?.GetMedicalRecordNumber(),
                    BirthDate =
                        patient?.BirthDate is { } bd
                        && DateTimeOffset.TryParse(bd, out var birthDate)
                            ? birthDate
                            : null,
                    Gender = EnumUtility.GetLiteral(patient?.Gender),
                    Phone = patient
                        ?.Telecom?.FirstOrDefault(t =>
                            t.System == ContactPoint.ContactPointSystem.Phone
                        )
                        ?.Value,
                    Email = patient
                        ?.Telecom?.FirstOrDefault(t =>
                            t.System == ContactPoint.ContactPointSystem.Email
                        )
                        ?.Value,
                    Status = status,
                    RecommendedDate = recommendedDate,
                    SystemDeterminedIneligible = isFlaggedIneligible,
                    LastUpdated = subject.Meta?.LastUpdated,
                }
            );
        }

        var (recruited, pending, notRecruited) = CountByStatus(
            patientEntries.Select(p => p.Status)
        );

        var summary = new TrialSummaryDto
        {
            ListId = list.Id!,
            TrialIdentifier = trialIdentifier,
            StudyAcronym = acronym,
            StudyTitle = study?.Title,
            StudyDescription = study?.Description,
            Criteria = criteria,
            ListStatus =
                EnumUtility.GetLiteral(list.Status)
                ?? RecruIT.List.Services.Fhir.FhirConstants.ListStatusCurrent,
            RecruitedCount = recruited,
            PendingCount = pending,
            NotRecruitedCount = notRecruited,
        };

        return (summary, patientEntries.OrderByDescending(p => p.RecommendedDate).ToList());
    }

    public async Task UpdateListStatusAsync(
        string listId,
        string status,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        if (!accessService.CanPatchList(user))
        {
            throw new UnauthorizedAccessException(localizer["App.Errors.AdminOnlyTrialStatus"]);
        }

        var client = clientFactory.CreateClient();
        var patchDocument = $$"""[{"op":"replace","path":"/status","value":"{{status}}"}]""";

        try
        {
            // PatchAsync<TResource> builds the "{ResourceType}/{id}" path itself from TResource, so
            // the id argument must be bare (no "List/" prefix) or the two collide into an invalid
            // "List/List" path.
            await client.PatchAsync<FhirList>(listId, patchDocument, ResourceFormat.Json, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update status for List/{ListId}", listId);
            throw new FhirAccessException(localizer["App.Errors.TrialStatusUpdateFailed"], ex);
        }
    }

    /// <summary>
    /// Resolves the current FHIR List id (and acronym) for a trial identified by its business
    /// identifier token ("system|value" - see TrialIdentifier), for building a link back into this
    /// app. Deliberately not cached anywhere - the FHIR List's logical id is server-local and can
    /// change (e.g. if a study's screening list is ever recreated), unlike the trial identifier
    /// itself, so callers that only have the token (the notification subsystem persists that, not
    /// the List id - see NotificationEvent) resolve it fresh here. Not access-checked: callers must
    /// already know the caller is entitled to see this trial.
    /// </summary>
    public async Task<(string ListId, string StudyAcronym)?> ResolveListForTrialAsync(
        string trialIdentifierToken,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        var studyResources = await FhirBundleHelpers.GetAllPagesAsync(
            client,
            $"ResearchStudy?identifier={Uri.EscapeDataString(trialIdentifierToken)}",
            ct
        );
        var study = studyResources.OfType<ResearchStudy>().FirstOrDefault();
        if (study?.Id is null)
        {
            return null;
        }

        var listResources = await FhirBundleHelpers.GetAllPagesAsync(
            client,
            $"List?belongs-to-study=ResearchStudy/{study.Id}",
            ct
        );
        var list = listResources.OfType<FhirList>().FirstOrDefault();
        if (list?.Id is null)
        {
            return null;
        }

        return (list.Id, study.GetStudyAcronym() ?? list.Id);
    }

    /// <summary>
    /// The List?code=...&amp;_include=... query shared by every method that scans screening lists
    /// across trials (dashboard, patient overview, notifications) - only the status filter varies.
    /// </summary>
    private static string BuildScreeningListsQuery(string status) =>
        $"List?code={Uri.EscapeDataString($"{RecruIT.List.Services.Fhir.FhirConstants.SystemScreeningList}|{RecruIT.List.Services.Fhir.FhirConstants.ScreeningListCode}")}"
        + $"&status={status}"
        + "&_include=List:item"
        + "&_include:iterate=ResearchSubject:patient"
        + "&_include=List:belongs-to-study"
        + "&_count=100";

    /// <summary>
    /// Filters lists down to those whose trial the user can access, paired with the TrialInfo
    /// resolved for each - the "resolve, then skip if unauthorized" step every cross-trial scan
    /// needs before it can build its own view-specific DTOs.
    /// </summary>
    private static IEnumerable<(FhirList List, TrialInfo Info)> GetAccessibleLists(
        IEnumerable<FhirList> lists,
        IReadOnlyDictionary<string, TrialInfo> trialInfoByListId,
        IReadOnlySet<TrialIdentifier>? accessibleTrials
    )
    {
        foreach (var list in lists)
        {
            var trialInfo = list.Id is not null
                ? trialInfoByListId.GetValueOrDefault(list.Id)
                : null;
            if (
                trialInfo is null
                || string.IsNullOrEmpty(trialInfo.Acronym)
                || !TrialAccessService.CanAccessTrial(accessibleTrials, trialInfo.Identifier)
            )
            {
                continue;
            }

            yield return (list, trialInfo);
        }
    }

    /// <summary>Buckets ResearchSubject statuses into the three counts every trial/list summary shows.</summary>
    private static (int Recruited, int Pending, int NotRecruited) CountByStatus(
        IEnumerable<string?> statuses
    )
    {
        int recruited = 0,
            pending = 0,
            notRecruited = 0;

        foreach (var status in statuses)
        {
            if (
                status is not null
                && RecruIT.List.Services.Fhir.FhirConstants.RecruitedStatuses.Contains(status)
            )
            {
                recruited++;
            }
            else if (
                status is not null
                && RecruIT.List.Services.Fhir.FhirConstants.NotRecruitedStatuses.Contains(status)
            )
            {
                notRecruited++;
            }
            else
            {
                pending++;
            }
        }

        return (recruited, pending, notRecruited);
    }

    /// <summary>
    /// Resolves each List's ResearchStudy - its business identifier (see TrialIdentifier) and its
    /// acronym, read live off the ResearchStudy rather than trusting a Reference.display text
    /// cached on the List - from ResearchStudy resources already pulled into the same response via
    /// the belongs-to-study SearchParameter (see src/hack/fhir/search-parameters-transaction.json),
    /// so this needs no request of its own.
    /// </summary>
    private static Dictionary<string, TrialInfo> BuildTrialInfoByListId(
        IReadOnlyList<Resource> resources,
        IEnumerable<FhirList> lists
    )
    {
        var studiesById = resources
            .OfType<ResearchStudy>()
            .Where(s => s.Id is not null)
            .ToDictionary(s => s.Id!, s => s);

        var result = new Dictionary<string, TrialInfo>();
        foreach (var list in lists)
        {
            var studyId = list.GetReferenceExtension(FhirConstants.UrlListBelongsToStudy)
                ?.GetReferencedId();
            if (
                list.Id is null
                || studyId is null
                || !studiesById.TryGetValue(studyId, out var study)
            )
            {
                continue;
            }

            if (study.GetTrialIdentifier() is { } identifier)
            {
                result[list.Id] = new TrialInfo(identifier, study.GetStudyAcronym(), study.Title);
            }
        }

        return result;
    }

    private sealed record TrialInfo(TrialIdentifier Identifier, string? Acronym, string? Title);

    private static string? FormatPatientName(Patient? patient)
    {
        var name = patient?.Name?.FirstOrDefault();
        if (name is null)
        {
            return null;
        }

        var given = string.Join(' ', name.Given ?? []);
        return string.Join(
            ' ',
            new[] { given, name.Family }.Where(s => !string.IsNullOrWhiteSpace(s))
        );
    }
}
