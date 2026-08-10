using System.Security.Claims;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Utility;
using list.Models;
using list.Resources;
using list.Services.Access;
using Microsoft.Extensions.Localization;
using FhirList = Hl7.Fhir.Model.List;
using Task = System.Threading.Tasks.Task;

namespace list.Services.Fhir;

public sealed class ScreeningListService(
    FhirClientFactory clientFactory,
    TrialAccessService accessService,
    IStringLocalizer<SharedResources> localizer,
    ILogger<ScreeningListService> logger)
{
    public async Task<IReadOnlyList<TrialSummaryDto>> GetAccessibleTrialsAsync(
        ClaimsPrincipal user, int newSuggestionWindowDays, int stalledLeadWindowDays, CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();

        var query =
            $"List?code={Uri.EscapeDataString($"{FhirConstants.SystemScreeningList}|{FhirConstants.ScreeningListCode}")}" +
            "&status=current,retired" +
            "&_include=List:item" +
            "&_include:iterate=ResearchSubject:patient" +
            "&_count=100";

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

        var researchSubjectsById = resources.OfType<ResearchSubject>().ToDictionary(rs => rs.Id!, rs => rs);
        var lists = resources.OfType<FhirList>().ToList();

        var trialIdentifiers = await ResolveTrialIdentifiersAsync(client, lists, ct);
        var accessibleTrials = await accessService.GetAccessibleTrialIdentifiersAsync(user, ct);

        var summaries = new List<TrialSummaryDto>();
        foreach (var list in lists)
        {
            var acronym = list.GetReferenceExtension(FhirConstants.UrlListBelongsToStudy)?.Display;
            var trialIdentifier = list.Id is not null ? trialIdentifiers.GetValueOrDefault(list.Id) : null;
            if (string.IsNullOrEmpty(acronym) || !TrialAccessService.CanAccessTrial(accessibleTrials, trialIdentifier))
            {
                continue;
            }

            var entries = list.Entry ?? [];
            int recruited = 0, pending = 0, notRecruited = 0, newSuggestions = 0, stalled = 0;

            foreach (var entry in entries)
            {
                var subjectId = entry.Item?.Reference?.Split('/').LastOrDefault();
                var subject = subjectId is not null && researchSubjectsById.TryGetValue(subjectId, out var s) ? s : null;
                var status = subject is not null ? EnumUtility.GetLiteral(subject.Status) : null;

                if (status is not null && FhirConstants.RecruitedStatuses.Contains(status))
                {
                    recruited++;
                }
                else if (status is not null && FhirConstants.NotRecruitedStatuses.Contains(status))
                {
                    notRecruited++;
                }
                else
                {
                    pending++;

                    if (subject?.Meta?.LastUpdated is { } lastUpdated &&
                        lastUpdated < DateTimeOffset.UtcNow.AddDays(-stalledLeadWindowDays))
                    {
                        stalled++;
                    }
                }

                if (entry.Date is { } dateString && DateTimeOffset.TryParse(dateString, out var recommendedDate) &&
                    recommendedDate >= DateTimeOffset.UtcNow.AddDays(-newSuggestionWindowDays))
                {
                    newSuggestions++;
                }
            }

            var truncationNote = list.Note?.FirstOrDefault()?.Text;

            summaries.Add(new TrialSummaryDto
            {
                ListId = list.Id!,
                TrialIdentifier = trialIdentifier!,
                StudyAcronym = acronym,
                ListStatus = EnumUtility.GetLiteral(list.Status) ?? FhirConstants.ListStatusCurrent,
                LastUpdated = list.Meta?.LastUpdated,
                TruncationNote = truncationNote,
                RecruitedCount = recruited,
                PendingCount = pending,
                NotRecruitedCount = notRecruited,
                NewSuggestionsCount = newSuggestions,
                StalledLeadsCount = stalled,
            });
        }

        return summaries.OrderBy(s => s.StudyAcronym, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Patient-centric view of the same data the dashboard uses: instead of one row per trial, this
    /// groups every accessible List's entries by patient, so a patient recommended for several
    /// trials shows up once with a membership entry per trial ("potentially fitting studies").
    /// </summary>
    public async Task<IReadOnlyList<PatientOverviewDto>> GetPatientsAcrossTrialsAsync(
        ClaimsPrincipal user, CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();

        var query =
            $"List?code={Uri.EscapeDataString($"{FhirConstants.SystemScreeningList}|{FhirConstants.ScreeningListCode}")}" +
            "&status=current" +
            "&_include=List:item" +
            "&_include:iterate=ResearchSubject:patient" +
            "&_count=100";

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

        var trialIdentifiers = await ResolveTrialIdentifiersAsync(client, lists, ct);
        var accessibleTrials = await accessService.GetAccessibleTrialIdentifiersAsync(user, ct);

        var overviewsByPatient = new Dictionary<string, PatientOverviewDto>();

        foreach (var list in lists)
        {
            var acronym = list.GetReferenceExtension(FhirConstants.UrlListBelongsToStudy)?.Display;
            var trialIdentifier = list.Id is not null ? trialIdentifiers.GetValueOrDefault(list.Id) : null;
            if (string.IsNullOrEmpty(acronym) || !TrialAccessService.CanAccessTrial(accessibleTrials, trialIdentifier))
            {
                continue;
            }

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
                        BirthDate = patient?.BirthDate is { } bd && DateTimeOffset.TryParse(bd, out var birthDate) ? birthDate : null,
                        Gender = EnumUtility.GetLiteral(patient?.Gender),
                        Trials = [],
                    };
                    overviewsByPatient[patientId] = overview;
                }

                DateTimeOffset? recommendedDate = entry.Date is { } d && DateTimeOffset.TryParse(d, out var parsed) ? parsed : null;
                var isFlaggedIneligible = entry.Flag?.Coding?.Any(c =>
                    c.System == FhirConstants.SystemDeterminedSubjectStatus && c.Code == FhirConstants.DeterminedStatusIneligible) ?? false;

                overview.Trials.Add(new PatientListEntryDto
                {
                    ResearchSubjectId = subject.Id!,
                    PatientId = patientId,
                    Name = overview.Name,
                    BirthDate = overview.BirthDate,
                    Gender = overview.Gender,
                    Phone = patient?.Telecom?.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone)?.Value,
                    Email = patient?.Telecom?.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email)?.Value,
                    Status = EnumUtility.GetLiteral(subject.Status) ?? "candidate",
                    Note = subject.GetStringExtension(FhirConstants.UrlResearchSubjectNote),
                    RecommendedDate = recommendedDate,
                    SystemDeterminedIneligible = isFlaggedIneligible,
                    LastUpdated = subject.Meta?.LastUpdated,
                    ListId = list.Id,
                    StudyAcronym = acronym,
                    TrialIdentifier = trialIdentifier,
                });
            }
        }

        foreach (var overview in overviewsByPatient.Values)
        {
            overview.Trials.Sort((a, b) => string.Compare(a.StudyAcronym, b.StudyAcronym, StringComparison.OrdinalIgnoreCase));
        }

        return overviewsByPatient.Values
            .OrderBy(o => o.Name ?? o.PatientId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Flattens new-recommendation entries (List.entry.date within the configured window) across
    /// every trial the user can access, for the notification view. Computed on-demand from the
    /// same data the dashboard uses - there is no separate background scan, since a Blazor Server
    /// background service has no per-user OIDC token to call FHIR with.
    /// </summary>
    public async Task<IReadOnlyList<NotificationEventDto>> GetNewRecommendationEventsAsync(
        ClaimsPrincipal user, int windowDays, CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();

        var query =
            $"List?code={Uri.EscapeDataString($"{FhirConstants.SystemScreeningList}|{FhirConstants.ScreeningListCode}")}" +
            "&status=current" +
            "&_include=List:item" +
            "&_include:iterate=ResearchSubject:patient" +
            "&_count=100";

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(client, query, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch screening lists for notifications");
            throw new FhirAccessException(localizer["App.Errors.NotificationsLoadFailed"], ex);
        }

        var subjectsById = resources.OfType<ResearchSubject>().ToDictionary(rs => rs.Id!, rs => rs);
        var patientsById = resources.OfType<Patient>().ToDictionary(p => p.Id!, p => p);
        var lists = resources.OfType<FhirList>().ToList();
        var events = new List<NotificationEventDto>();

        var trialIdentifiers = await ResolveTrialIdentifiersAsync(client, lists, ct);
        var accessibleTrials = await accessService.GetAccessibleTrialIdentifiersAsync(user, ct);

        foreach (var list in lists)
        {
            var acronym = list.GetReferenceExtension(FhirConstants.UrlListBelongsToStudy)?.Display;
            var trialIdentifier = list.Id is not null ? trialIdentifiers.GetValueOrDefault(list.Id) : null;
            if (string.IsNullOrEmpty(acronym) || !TrialAccessService.CanAccessTrial(accessibleTrials, trialIdentifier))
            {
                continue;
            }

            foreach (var entry in list.Entry ?? [])
            {
                if (entry.Date is not { } dateString || !DateTimeOffset.TryParse(dateString, out var recommendedDate) ||
                    recommendedDate < DateTimeOffset.UtcNow.AddDays(-windowDays))
                {
                    continue;
                }

                var subjectId = entry.Item?.Reference?.Split('/').LastOrDefault();
                var subject = subjectId is not null && subjectsById.TryGetValue(subjectId, out var s) ? s : null;
                var patientId = subject?.Individual?.Reference?.Split('/').LastOrDefault();
                var patient = patientId is not null && patientsById.TryGetValue(patientId, out var p) ? p : null;
                var patientName = FormatPatientName(patient) ?? patientId ?? localizer["App.Notifications.UnknownPatient"].Value;

                events.Add(new NotificationEventDto
                {
                    Id = $"new:{subjectId}",
                    Kind = NotificationKind.NewRecommendation,
                    ListId = list.Id!,
                    TrialIdentifier = trialIdentifier!,
                    StudyAcronym = acronym,
                    PatientId = patientId,
                    PatientName = patientName,
                    Message = localizer["App.Notifications.MessageFormat", patientName, acronym],
                    OccurredAt = recommendedDate,
                });
            }
        }

        return events.OrderByDescending(e => e.OccurredAt).ToList();
    }

    public async Task<(TrialSummaryDto Summary, IReadOnlyList<PatientListEntryDto> Patients)> GetListWithPatientsAsync(
        string listId, ClaimsPrincipal user, int newSuggestionWindowDays, int stalledLeadWindowDays, CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();

        var query = $"List?_id={Uri.EscapeDataString(listId)}&_include=List:item&_include:iterate=ResearchSubject:patient";

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

        var list = resources.OfType<FhirList>().FirstOrDefault()
            ?? throw new FhirAccessException(localizer["App.Errors.ListNotFound"]);

        var belongsToStudyRef = list.GetReferenceExtension(FhirConstants.UrlListBelongsToStudy);
        var acronym = belongsToStudyRef?.Display;

        // The acronym comes from the extension's display text (no _include exists for arbitrary
        // extension references), and the trial identifier used for access checks (see
        // TrialIdentifier) plus the title/description all live on the ResearchStudy itself, so
        // fetch it once up front - before the access check, since the check now needs it too.
        ResearchStudy? study = null;
        if (belongsToStudyRef?.Reference is { } studyReference)
        {
            try
            {
                study = await client.GetAsync(studyReference, ct) as ResearchStudy;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch {StudyReference} for List/{ListId}", studyReference, listId);
            }
        }

        var trialIdentifier = study?.GetTrialIdentifier();
        if (string.IsNullOrEmpty(acronym) || trialIdentifier is null || !await accessService.CanAccessTrialAsync(user, trialIdentifier, ct))
        {
            throw new UnauthorizedAccessException(localizer["App.Errors.NotAuthorizedTrial"]);
        }

        var subjectsById = resources.OfType<ResearchSubject>().ToDictionary(rs => rs.Id!, rs => rs);
        var patientsById = resources.OfType<Patient>().ToDictionary(p => p.Id!, p => p);

        var patientEntries = new List<PatientListEntryDto>();
        int recruited = 0, pending = 0, notRecruited = 0, newSuggestions = 0, stalled = 0;

        foreach (var entry in list.Entry ?? [])
        {
            var subjectId = entry.Item?.Reference?.Split('/').LastOrDefault();
            if (subjectId is null || !subjectsById.TryGetValue(subjectId, out var subject))
            {
                continue;
            }

            var patientId = subject.Individual?.Reference?.Split('/').LastOrDefault();
            var patient = patientId is not null && patientsById.TryGetValue(patientId, out var p) ? p : null;

            var status = EnumUtility.GetLiteral(subject.Status) ?? "candidate";
            DateTimeOffset? recommendedDate = entry.Date is { } d && DateTimeOffset.TryParse(d, out var parsed) ? parsed : null;
            var isFlaggedIneligible = entry.Flag?.Coding?.Any(c =>
                c.System == FhirConstants.SystemDeterminedSubjectStatus && c.Code == FhirConstants.DeterminedStatusIneligible) ?? false;

            if (FhirConstants.RecruitedStatuses.Contains(status))
            {
                recruited++;
            }
            else if (FhirConstants.NotRecruitedStatuses.Contains(status))
            {
                notRecruited++;
            }
            else
            {
                pending++;

                if (subject.Meta?.LastUpdated is { } lastUpdated &&
                    lastUpdated < DateTimeOffset.UtcNow.AddDays(-stalledLeadWindowDays))
                {
                    stalled++;
                }
            }

            if (recommendedDate is not null && recommendedDate >= DateTimeOffset.UtcNow.AddDays(-newSuggestionWindowDays))
            {
                newSuggestions++;
            }

            patientEntries.Add(new PatientListEntryDto
            {
                ResearchSubjectId = subject.Id!,
                PatientId = patientId ?? string.Empty,
                Name = FormatPatientName(patient),
                BirthDate = patient?.BirthDate is { } bd && DateTimeOffset.TryParse(bd, out var birthDate) ? birthDate : null,
                Gender = EnumUtility.GetLiteral(patient?.Gender),
                Phone = patient?.Telecom?.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone)?.Value,
                Email = patient?.Telecom?.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email)?.Value,
                Status = status,
                Note = subject.GetStringExtension(FhirConstants.UrlResearchSubjectNote),
                RecommendedDate = recommendedDate,
                SystemDeterminedIneligible = isFlaggedIneligible,
                LastUpdated = subject.Meta?.LastUpdated,
            });
        }

        var summary = new TrialSummaryDto
        {
            ListId = list.Id!,
            TrialIdentifier = trialIdentifier,
            StudyAcronym = acronym,
            StudyTitle = study?.Title,
            StudyDescription = study?.Description,
            ListStatus = EnumUtility.GetLiteral(list.Status) ?? FhirConstants.ListStatusCurrent,
            LastUpdated = list.Meta?.LastUpdated,
            TruncationNote = list.Note?.FirstOrDefault()?.Text,
            RecruitedCount = recruited,
            PendingCount = pending,
            NotRecruitedCount = notRecruited,
            NewSuggestionsCount = newSuggestions,
            StalledLeadsCount = stalled,
        };

        return (summary, patientEntries.OrderByDescending(p => p.RecommendedDate).ToList());
    }

    public async Task UpdateListStatusAsync(string listId, string status, ClaimsPrincipal user, CancellationToken ct = default)
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
    /// Resolves each List's ResearchStudy business identifier (see TrialIdentifier) in one batched
    /// `_id=a,b,c` fetch instead of one request per list, keyed by the List's own FHIR id.
    /// </summary>
    private async Task<Dictionary<string, TrialIdentifier>> ResolveTrialIdentifiersAsync(
        FhirClient client, IEnumerable<FhirList> lists, CancellationToken ct)
    {
        var studyIdByListId = new Dictionary<string, string>();
        foreach (var list in lists)
        {
            var studyId = list.GetReferenceExtension(FhirConstants.UrlListBelongsToStudy)?.GetReferencedId();
            if (list.Id is not null && studyId is not null)
            {
                studyIdByListId[list.Id] = studyId;
            }
        }

        if (studyIdByListId.Count == 0)
        {
            return [];
        }

        var distinctStudyIds = studyIdByListId.Values.Distinct().ToList();
        var query = $"ResearchStudy?_id={Uri.EscapeDataString(string.Join(',', distinctStudyIds))}&_count={distinctStudyIds.Count}";

        List<Resource> studyResources;
        try
        {
            studyResources = await FhirBundleHelpers.GetAllPagesAsync(client, query, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to batch-resolve ResearchStudy identifiers for {Count} studies", distinctStudyIds.Count);
            return [];
        }

        var identifierByStudyId = studyResources.OfType<ResearchStudy>()
            .Where(s => s.Id is not null)
            .Select(s => (s.Id, Identifier: s.GetTrialIdentifier()))
            .Where(t => t.Identifier is not null)
            .ToDictionary(t => t.Id!, t => t.Identifier!);

        var result = new Dictionary<string, TrialIdentifier>();
        foreach (var (listId, studyId) in studyIdByListId)
        {
            if (identifierByStudyId.TryGetValue(studyId, out var identifier))
            {
                result[listId] = identifier;
            }
        }

        return result;
    }

    private static string? FormatPatientName(Patient? patient)
    {
        var name = patient?.Name?.FirstOrDefault();
        if (name is null)
        {
            return null;
        }

        var given = string.Join(' ', name.Given ?? []);
        return string.Join(' ', new[] { given, name.Family }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
