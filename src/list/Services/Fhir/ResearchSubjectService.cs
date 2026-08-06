using System.Security.Claims;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Utility;
using list.Services.Access;
using Task = System.Threading.Tasks.Task;

namespace list.Services.Fhir;

public sealed class ResearchSubjectHistoryEntryDto
{
    public required string Status { get; init; }

    public string? Note { get; init; }

    public required DateTimeOffset LastUpdated { get; init; }

    public required int VersionId { get; init; }
}

public sealed class ResearchSubjectService(
    FhirClientFactory clientFactory,
    TrialAccessService accessService,
    ILogger<ResearchSubjectService> logger)
{
    public async Task UpdateStatusAsync(
        string researchSubjectId, string newStatus, string studyAcronym, ClaimsPrincipal user, CancellationToken ct = default)
    {
        EnsureCanPatch(studyAcronym, user);

        var patch = JsonSerializer.Serialize(new object[]
        {
            new { op = "replace", path = "/status", value = newStatus },
        });

        await PatchAsync(researchSubjectId, patch, ct).ConfigureAwait(false);
    }

    public async Task UpdateNoteAsync(
        string researchSubjectId, string note, string studyAcronym, ClaimsPrincipal user, CancellationToken ct = default)
    {
        EnsureCanPatch(studyAcronym, user);

        var patch = JsonSerializer.Serialize(new object[]
        {
            new
            {
                op = "add",
                path = "/extension",
                value = new object[]
                {
                    new { url = FhirConstants.UrlResearchSubjectNote, valueString = note },
                },
            },
        });

        await PatchAsync(researchSubjectId, patch, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResearchSubjectHistoryEntryDto>> GetHistoryAsync(string researchSubjectId, CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(client, $"ResearchSubject/{researchSubjectId}/_history", ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch history for ResearchSubject/{ResearchSubjectId}", researchSubjectId);
            throw new FhirAccessException("The status history for this patient could not be loaded.", ex);
        }

        return resources.OfType<ResearchSubject>()
            .Where(rs => rs.Meta?.LastUpdated is not null)
            .OrderByDescending(rs => rs.Meta!.LastUpdated)
            .Select(rs => new ResearchSubjectHistoryEntryDto
            {
                Status = EnumUtility.GetLiteral(rs.Status) ?? "candidate",
                Note = rs.GetStringExtension(FhirConstants.UrlResearchSubjectNote),
                LastUpdated = rs.Meta!.LastUpdated!.Value,
                VersionId = int.TryParse(rs.Meta.VersionId, out var v) ? v : 0,
            })
            .ToList();
    }

    private void EnsureCanPatch(string studyAcronym, ClaimsPrincipal user)
    {
        if (!accessService.CanPatchResearchSubject(user, studyAcronym))
        {
            throw new UnauthorizedAccessException("You are not authorized to update patients for this trial.");
        }
    }

    private async Task PatchAsync(string researchSubjectId, string patchDocument, CancellationToken ct)
    {
        var client = clientFactory.CreateClient();
        try
        {
            // PatchAsync<TResource> builds the "{ResourceType}/{id}" path itself from TResource, so
            // the id argument must be bare (no "ResearchSubject/" prefix) or the two collide into an
            // invalid "ResearchSubject/ResearchSubject" path.
            await client.PatchAsync<ResearchSubject>(researchSubjectId, patchDocument, ResourceFormat.Json, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to PATCH ResearchSubject/{ResearchSubjectId}", researchSubjectId);
            throw new FhirAccessException("The patient's record could not be updated. Please try again.", ex);
        }
    }
}
