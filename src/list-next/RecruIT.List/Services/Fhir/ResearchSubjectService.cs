using System.Net;
using System.Security.Claims;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Utility;
using RecruIT.List.Models;
using RecruIT.List.Resources;
using RecruIT.List.Services;
using RecruIT.List.Services.Access;
using Microsoft.Extensions.Localization;
using Task = System.Threading.Tasks.Task;

namespace RecruIT.List.Services.Fhir;

public sealed class ResearchSubjectHistoryEntryDto
{
    public required string Status { get; init; }

    public required DateTimeOffset LastUpdated { get; init; }

    public required int VersionId { get; init; }
}

public sealed class ResearchSubjectService(
    FhirClientFactory clientFactory,
    TrialAccessService accessService,
    ScreeningNoteService screeningNoteService,
    IStringLocalizer<SharedResources> localizer,
    ILogger<ResearchSubjectService> logger
)
{
    /// <summary>
    /// Updates a subject's status via a version-aware read-modify-write (PUT with If-Match), not
    /// PATCH - Blaze, among other servers, doesn't implement the PATCH verb at all. Being
    /// version-aware also buys optimistic-concurrency conflict detection for free: if someone else
    /// updated this subject after it was read here, the server rejects the write instead of
    /// silently clobbering their change, and that's surfaced to the caller as a
    /// <see cref="FhirAccessException"/> distinct from a generic failure.
    /// </summary>
    /// <returns>The subject's new meta.lastUpdated, as reported by the FHIR server's update response.</returns>
    public async Task<DateTimeOffset?> UpdateStatusAsync(
        string researchSubjectId,
        string newStatus,
        TrialIdentifier trialIdentifier,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        await EnsureCanPatchAsync(trialIdentifier, user, ct);

        var client = clientFactory.CreateClient();

        ResearchSubject current;
        try
        {
            current =
                await client.ReadAsync<ResearchSubject>(
                    $"ResearchSubject/{researchSubjectId}",
                    ct: ct
                ) ?? throw new FhirAccessException(localizer["App.Errors.PatientRecordNotFound"]);
        }
        catch (FhirAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to read ResearchSubject/{ResearchSubjectId} before updating status",
                researchSubjectId
            );
            throw new FhirAccessException(
                localizer["App.Errors.PatientRecordLoadForStatusUpdateFailed"],
                ex
            );
        }

        current.Status = EnumUtility.ParseLiteral<ResearchSubject.ResearchSubjectStatus>(
            newStatus,
            ignoreCase: true
        );

        try
        {
            var updated = await client.UpdateAsync(current, versionAware: true, ct);
            return updated?.Meta?.LastUpdated;
        }
        catch (FhirOperationException ex)
            when (ex.Status is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            logger.LogWarning(
                ex,
                "Version conflict updating status for ResearchSubject/{ResearchSubjectId}: it was "
                    + "changed by someone else since it was last loaded",
                researchSubjectId
            );
            throw new FhirAccessException(localizer["App.Errors.PatientRecordStatusConflict"], ex);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to update status for ResearchSubject/{ResearchSubjectId}",
                researchSubjectId
            );
            throw new FhirAccessException(localizer["App.Errors.PatientRecordUpdateFailed"], ex);
        }
    }

    /// <summary>Status-only change history; building block for GetTimelineAsync's merged status+note feed.</summary>
    private async Task<IReadOnlyList<ResearchSubjectHistoryEntryDto>> GetHistoryAsync(
        string researchSubjectId,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(
                client,
                $"ResearchSubject/{researchSubjectId}/_history",
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to fetch history for ResearchSubject/{ResearchSubjectId}",
                researchSubjectId
            );
            throw new FhirAccessException(localizer["App.Errors.StatusHistoryLoadFailed"], ex);
        }

        var versions = resources
            .OfType<ResearchSubject>()
            .Where(rs => rs.Meta?.LastUpdated is not null)
            .Select(rs => new ResearchSubjectHistoryEntryDto
            {
                Status = EnumUtility.GetLiteral(rs.Status) ?? "candidate",
                LastUpdated = rs.Meta!.LastUpdated!.Value,
                VersionId = int.TryParse(rs.Meta.VersionId, out var v) ? v : 0,
            })
            .OrderBy(h => h.VersionId)
            .ToList();

        // Historical versions written before screening notes moved to Postgres can still include
        // note-only edits that bumped this resource's version without changing /status (this app
        // no longer writes anything to ResearchSubject except status changes, so this only matters
        // for that older history). Only surface an entry where the status actually differs from
        // the version right before it, so those don't show up as spurious "status changed to X"
        // (X being whatever it already was) entries.
        var changes = new List<ResearchSubjectHistoryEntryDto>();
        string? previousStatus = null;
        foreach (var version in versions)
        {
            if (version.Status != previousStatus)
            {
                changes.Add(version);
            }

            previousStatus = version.Status;
        }

        return changes.OrderByDescending(h => h.LastUpdated).ToList();
    }

    /// <summary>Status changes and added notes merged into one newest-first timeline, for the patient history view.</summary>
    public async Task<IReadOnlyList<PatientHistoryEntryDto>> GetTimelineAsync(
        string researchSubjectId,
        string? researchSubjectIdentifier,
        CancellationToken ct = default
    )
    {
        var historyTask = GetHistoryAsync(researchSubjectId, ct);
        var notesTask = researchSubjectIdentifier is not null
            ? screeningNoteService.GetNotesAsync(researchSubjectIdentifier, ct)
            : Task.FromResult<IReadOnlyList<ScreeningNoteDto>>([]);
        await Task.WhenAll(historyTask, notesTask);

        var entries = new List<PatientHistoryEntryDto>();

        entries.AddRange(
            historyTask.Result.Select(h => new PatientHistoryEntryDto
            {
                Time = h.LastUpdated,
                Title = localizer[
                    "App.Timeline.StatusChanged",
                    localizer[StatusDisplay.GetKey(h.Status)]
                ],
                Kind = PatientHistoryEntryKind.StatusChange,
            })
        );

        entries.AddRange(
            notesTask
                .Result.Where(n => n.Time is not null)
                .Select(n => new PatientHistoryEntryDto
                {
                    Time = n.Time!.Value,
                    Title = string.IsNullOrEmpty(n.Author)
                        ? localizer["App.Timeline.NoteAdded"]
                        : localizer["App.Timeline.NoteAddedBy", n.Author],
                    Description = n.Text,
                    Kind = PatientHistoryEntryKind.Note,
                })
        );

        return entries.OrderByDescending(e => e.Time).ToList();
    }

    private async Task EnsureCanPatchAsync(
        TrialIdentifier trialIdentifier,
        ClaimsPrincipal user,
        CancellationToken ct
    )
    {
        if (!await accessService.CanPatchResearchSubjectAsync(user, trialIdentifier, ct))
        {
            throw new UnauthorizedAccessException(
                localizer["App.Errors.NotAuthorizedUpdatePatient"]
            );
        }
    }
}
