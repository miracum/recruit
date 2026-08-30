using Hl7.Fhir.Model;
using Microsoft.Extensions.Localization;
using RecruIT.FhirConstants;
using RecruIT.List.Models;
using RecruIT.List.Resources;

namespace RecruIT.List.Services.Fhir;

/// <summary>
/// Reads a study's eligibility attrition funnel: how many patients remain a candidate or unknown
/// after cumulatively applying each criterion, in Group.characteristic order. Computed by
/// query-sql-on-fhir and published as a Measure/MeasureReport pair (see
/// fhir/ig/input/fsh/eligibility-funnel.fsh) only when its own query-sql-on-fhir.funnel.enabled is
/// turned on - so a study with no MeasureReport yet is a normal, quiet "not available" here, not an
/// error.
/// </summary>
public sealed class EligibilityFunnelService(
    FhirClientFactory clientFactory,
    IStringLocalizer<SharedResources> localizer,
    ILogger<EligibilityFunnelService> logger
)
{
    private static readonly string TotalPopulationCode =
        Recruit.CodeSystems.EligibilityFunnelPopulationType.TotalPopulation.Code();

    /// <summary>
    /// The study's current funnel steps, in order, or null if none has been published yet (feature
    /// disabled for this deployment, or this study hasn't been polled since it was enabled).
    /// </summary>
    public async Task<IReadOnlyList<FunnelStepDto>?> GetFunnelAsync(
        string studyId,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(
                client,
                $"MeasureReport?belongs-to-study=ResearchStudy/{Uri.EscapeDataString(studyId)}",
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to fetch attrition funnel MeasureReport for ResearchStudy/{StudyId}",
                studyId
            );
            throw new FhirAccessException(localizer["App.Errors.FunnelLoadFailed"], ex);
        }

        var population = resources
            .OfType<MeasureReport>()
            .FirstOrDefault()
            ?.Group.FirstOrDefault()
            ?.Population;
        if (population is null || population.Count == 0)
        {
            return null;
        }

        return [.. population.Where(p => p.Count.HasValue).Select(ToFunnelStepDto)];
    }

    private static FunnelStepDto ToFunnelStepDto(MeasureReport.PopulationComponent population)
    {
        var isTotalPopulation =
            population.Code?.Coding?.Any(c => c.Code == TotalPopulationCode) is true;

        return new FunnelStepDto
        {
            Label = isTotalPopulation ? null : population.Code?.Text,
            Count = population.Count!.Value,
            IsTotalPopulation = isTotalPopulation,
        };
    }
}
