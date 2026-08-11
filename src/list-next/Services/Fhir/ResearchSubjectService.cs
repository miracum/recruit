using System.Security.Claims;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Utility;
using list.Models;
using list.Resources;
using list.Services.Access;
using list.Services.Auth;
using Microsoft.Extensions.Localization;
using Task = System.Threading.Tasks.Task;

namespace list.Services.Fhir;

public sealed class ResearchSubjectHistoryEntryDto
{
    public required string Status { get; init; }

    public required DateTimeOffset LastUpdated { get; init; }

    public required int VersionId { get; init; }
}

public sealed class ResearchSubjectService(
    FhirClientFactory clientFactory,
    TrialAccessService accessService,
    PractitionerService practitionerService,
    IStringLocalizer<SharedResources> localizer,
    ILogger<ResearchSubjectService> logger
)
{
    /// <returns>The subject's new meta.lastUpdated, as reported by the FHIR server's PATCH response.</returns>
    public async Task<DateTimeOffset?> UpdateStatusAsync(
        string researchSubjectId,
        string newStatus,
        TrialIdentifier trialIdentifier,
        ClaimsPrincipal user,
        CancellationToken ct = default
    )
    {
        await EnsureCanPatchAsync(trialIdentifier, user, ct);

        var patch = JsonSerializer.Serialize(
            new object[]
            {
                new
                {
                    op = "replace",
                    path = "/status",
                    value = newStatus,
                },
            }
        );

        return await PatchAsync(researchSubjectId, patch, ct);
    }

    /// <summary>
    /// Appends a new note to the subject's researchSubjectNote extension - a repeating FHIR
    /// Annotation (author + time + text), never overwriting previous notes. This is the
    /// FHIR-sanctioned way to add a field a resource doesn't natively have (R4 ResearchSubject has
    /// no .note element), and keeps every note - with real authorship - directly on the resource
    /// instead of relying on the server's _history endpoint (which may have limited retention).
    /// </summary>
    /// <returns>The subject's new meta.lastUpdated, as reported by the FHIR server's PATCH response.</returns>
    public async Task<DateTimeOffset?> AddNoteAsync(
        string researchSubjectId,
        string note,
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
                "Failed to read ResearchSubject/{ResearchSubjectId} before adding a note",
                researchSubjectId
            );
            throw new FhirAccessException(
                localizer["App.Errors.PatientRecordLoadForNoteFailed"],
                ex
            );
        }

        // Prefer a reference to the user's Practitioner resource (a real, resolvable join key) over
        // a plain string; the display name is still carried on the reference itself (Reference.display
        // is not a choice type, so this isn't a tradeoff) as a point-in-time fallback for when the
        // Practitioner can't be resolved later. Falls back to authorString only if no Practitioner
        // could be created/found (e.g. the user has no email claim to key one on).
        var practitionerReference = await practitionerService.EnsureCurrentPractitionerAsync(
            user,
            ct
        );
        var authorDisplayName = GetAuthorDisplayName(user);

        object annotation = practitionerReference is not null
            ? new
            {
                url = FhirConstants.UrlResearchSubjectNote,
                valueAnnotation = new
                {
                    text = note,
                    time = DateTimeOffset.UtcNow.ToString("O"),
                    authorReference = new
                    {
                        reference = practitionerReference.Reference,
                        display = authorDisplayName,
                    },
                },
            }
            : new
            {
                url = FhirConstants.UrlResearchSubjectNote,
                valueAnnotation = new
                {
                    text = note,
                    time = DateTimeOffset.UtcNow.ToString("O"),
                    authorString = authorDisplayName,
                },
            };

        // JSON Patch's "/extension/-" append syntax requires the array to already exist; if this is
        // the subject's very first extension of any kind, the array itself must be created first.
        var patch = current.Extension is { Count: > 0 }
            ? JsonSerializer.Serialize(
                new object[]
                {
                    new
                    {
                        op = "add",
                        path = "/extension/-",
                        value = annotation,
                    },
                }
            )
            : JsonSerializer.Serialize(
                new object[]
                {
                    new
                    {
                        op = "add",
                        path = "/extension",
                        value = new[] { annotation },
                    },
                }
            );

        return await PatchAsync(researchSubjectId, patch, ct);
    }

    /// <summary>All notes ever added to this subject, newest first.</summary>
    public async Task<IReadOnlyList<PatientNoteDto>> GetNotesAsync(
        string researchSubjectId,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        ResearchSubject subject;
        try
        {
            subject =
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
                "Failed to fetch notes for ResearchSubject/{ResearchSubjectId}",
                researchSubjectId
            );
            throw new FhirAccessException(localizer["App.Errors.PatientNotesLoadFailed"], ex);
        }

        var annotations = subject.GetAnnotationExtensions(FhirConstants.UrlResearchSubjectNote);

        var practitionerIds = annotations
            .Select(a => (a.Author as ResourceReference).GetReferencedId())
            .OfType<string>()
            .Distinct()
            .ToList();

        var practitionerNames =
            practitionerIds.Count > 0
                ? await ResolvePractitionerDisplayNamesAsync(client, practitionerIds, ct)
                : [];

        return annotations
            .Select(a => new PatientNoteDto
            {
                Text = a.Text ?? string.Empty,
                Author = ResolveAuthorDisplay(a, practitionerNames),
                Time = DateTimeOffset.TryParse(a.Time, out var time) ? time : null,
            })
            .OrderByDescending(n => n.Time)
            .ToList();
    }

    private static string? ResolveAuthorDisplay(
        Annotation annotation,
        IReadOnlyDictionary<string, string> practitionerNames
    )
    {
        if (annotation.Author is ResourceReference reference)
        {
            var id = reference.GetReferencedId();
            return id is not null && practitionerNames.TryGetValue(id, out var name)
                ? name
                : reference.Display;
        }

        return (annotation.Author as FhirString)?.Value;
    }

    /// <summary>
    /// Resolves current display names for a note's authorReference Practitioners in one search
    /// (rather than one GET per note) - falls back silently to each reference's own .display if the
    /// lookup fails, since author resolution shouldn't block viewing notes.
    /// </summary>
    private async Task<Dictionary<string, string>> ResolvePractitionerDisplayNamesAsync(
        FhirClient client,
        IReadOnlyList<string> practitionerIds,
        CancellationToken ct
    )
    {
        var names = new Dictionary<string, string>();
        try
        {
            var bundle = await client.SearchAsync<Practitioner>(
                new SearchParams().Add("_id", string.Join(',', practitionerIds)),
                ct
            );
            foreach (
                var practitioner in bundle?.Entry.Select(e => e.Resource).OfType<Practitioner>()
                    ?? []
            )
            {
                var name =
                    practitioner.Name?.FirstOrDefault()?.Text
                    ?? practitioner
                        .Telecom?.FirstOrDefault(t =>
                            t.System == ContactPoint.ContactPointSystem.Email
                        )
                        ?.Value;
                if (practitioner.Id is not null && name is not null)
                {
                    names[practitioner.Id] = name;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve practitioner display names for notes");
        }

        return names;
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

        // Every PATCH - including a note-only one - creates a new ResearchSubject version, even
        // when /status itself is untouched. Only surface an entry where the status actually
        // differs from the version right before it, so a note-only edit doesn't also show up as a
        // spurious "status changed to X" (X being whatever it already was) alongside the real
        // "note added" entry at the same timestamp.
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
        CancellationToken ct = default
    )
    {
        var historyTask = GetHistoryAsync(researchSubjectId, ct);
        var notesTask = GetNotesAsync(researchSubjectId, ct);
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

    private static string GetAuthorDisplayName(ClaimsPrincipal user) =>
        user.GetDisplayName() ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "unknown";

    private async Task<DateTimeOffset?> PatchAsync(
        string researchSubjectId,
        string patchDocument,
        CancellationToken ct
    )
    {
        var client = clientFactory.CreateClient();
        try
        {
            // PatchAsync<TResource> builds the "{ResourceType}/{id}" path itself from TResource, so
            // the id argument must be bare (no "ResearchSubject/" prefix) or the two collide into an
            // invalid "ResearchSubject/ResearchSubject" path.
            var updated = await client.PatchAsync<ResearchSubject>(
                researchSubjectId,
                patchDocument,
                ResourceFormat.Json,
                ct
            );
            return updated?.Meta?.LastUpdated;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to PATCH ResearchSubject/{ResearchSubjectId}",
                researchSubjectId
            );
            throw new FhirAccessException(localizer["App.Errors.PatientRecordUpdateFailed"], ex);
        }
    }
}
