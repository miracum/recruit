using Hl7.Fhir.Model;
using Microsoft.Extensions.Localization;
using RecruIT.List.Models;
using RecruIT.List.Resources;

namespace RecruIT.List.Services.Fhir;

public sealed class LatestLocationDto
{
    public required string LocationName { get; init; }

    public DateTimeOffset? EncounterStart { get; init; }
}

public sealed class PatientRecordService(
    FhirClientFactory clientFactory,
    IStringLocalizer<SharedResources> localizer,
    ILogger<PatientRecordService> logger
)
{
    /// <summary>
    /// Finds the most recent Encounter with a usable location for the patient. There is no bulk
    /// "latest per patient" FHIR query, so this is fetched per-patient/on-demand, mirroring
    /// list-old's fetchLatestEncounterWithLocation.
    /// </summary>
    public async Task<LatestLocationDto?> GetLatestLocationAsync(
        string patientId,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        // Deliberately a single-page fetch, not FhirBundleHelpers.GetAllPagesAsync: _count=5
        // + _sort=-date already gives us the only encounters we could ever use (we return on the
        // first one with a usable location), and a FHIR server re-runs _include=Encounter:location
        // on every page it emits - so following "next" links here would re-add the same Location
        // resources already seen on an earlier page, and the OfType<Location>().ToDictionary(...)
        // below would throw on the resulting duplicate id the moment a patient's encounters span
        // more than one page and share a location (common - that's most patients).
        Bundle? bundle;
        try
        {
            bundle =
                await client.GetAsync(
                    $"Encounter?subject=Patient/{patientId}&_count=5&_include=Encounter:location&_sort=-date&_pretty=false",
                    ct
                ) as Bundle;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch encounters for Patient/{PatientId}", patientId);
            return null;
        }

        var resources =
            bundle?.Entry.Where(e => e.Resource is not null).Select(e => e.Resource!).ToList()
            ?? [];

        var locationsById = resources
            .OfType<Location>()
            .ToDictionary(l => $"Location/{l.Id}", l => l);
        var encounters = resources
            .OfType<Encounter>()
            .OrderByDescending(e => e.Period?.Start)
            .ToList();

        foreach (var encounter in encounters)
        {
            var locationEntry = encounter
                .Location?.OrderByDescending(l => l.Period?.Start)
                .FirstOrDefault();

            string? locationName = null;
            if (
                locationEntry?.Location?.Reference is { } reference
                && locationsById.TryGetValue(reference, out var location)
            )
            {
                locationName = location.Name ?? localizer["App.Common.Unknown"];
            }
            else if (!string.IsNullOrEmpty(locationEntry?.Location?.Display))
            {
                locationName = locationEntry.Location.Display;
            }
            else if (!string.IsNullOrEmpty(encounter.ServiceProvider?.Display))
            {
                locationName = encounter.ServiceProvider.Display;
            }

            if (locationName is not null)
            {
                return new LatestLocationDto
                {
                    LocationName = locationName,
                    EncounterStart = FhirBundleHelpers.ParseFhirInstant(encounter.Period?.Start),
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Per-criterion eligibility status for a patient within one trial - the Observations
    /// EligibilityBundleBuilder emits per (patient, criterion), see
    /// docs/trino/eligibility-criteria-design.md. `focus:ResearchStudy.identifier=` is a standard
    /// chained reference search (no custom SearchParameter needed): it resolves the ResearchStudy
    /// by its business identifier (TrialIdentifier) directly, without a separate id lookup.
    /// </summary>
    public async Task<IReadOnlyList<CriterionStatusDto>> GetEligibilityCriteriaStatusAsync(
        string patientId,
        TrialIdentifier trialIdentifier,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        var query =
            $"Observation?subject=Patient/{patientId}"
            + $"&focus:ResearchStudy.identifier={Uri.EscapeDataString(trialIdentifier.ToToken())}"
            + $"&category={Uri.EscapeDataString($"{FhirConstants.SystemObservationCategory}|{FhirConstants.ObservationCategoryEligibilityAssessment}")}";

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(client, query, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to fetch eligibility criteria status for Patient/{PatientId}",
                patientId
            );
            return [];
        }

        return resources
            .OfType<Observation>()
            .Select(ToCriterionStatus)
            .OrderBy(c => c.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Maps the SNOMED CT "Yes/No/Unknown/Indeterminate (qualifier value)" coding EligibilityBundleBuilder
    /// writes (see docs/trino/eligibility-criteria-design.md) back to a plain Met/Indeterminate pair -
    /// Unknown and any unrecognized/missing coding both fall back to "unresolved, not indeterminate".
    /// </summary>
    private CriterionStatusDto ToCriterionStatus(Observation observation)
    {
        var valueConcept = observation.Value as CodeableConcept;
        var code = valueConcept?.Coding?.FirstOrDefault()?.Code;
        var (met, indeterminate) = code switch
        {
            FhirConstants.SnomedCodeYes => (true, false),
            FhirConstants.SnomedCodeNo => (false, false),
            FhirConstants.SnomedCodeIndeterminate => ((bool?)null, true),
            _ => ((bool?)null, false),
        };

        return new CriterionStatusDto
        {
            DisplayText = observation.Code?.Text ?? localizer["App.Common.Unknown"],
            Met = met,
            Indeterminate = indeterminate,
            Note = valueConcept?.Text,
        };
    }
}
