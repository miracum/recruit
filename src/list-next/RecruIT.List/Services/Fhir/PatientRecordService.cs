using System.Net;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
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
    /// Whether this FHIR server rejected "_sort=-date" on an Encounter search. Not every server can
    /// sort by it - Blaze only sorts on _id and _lastUpdated and answers anything else with a 400
    /// ("Unknown search-param `date` in sort clause"). Latched process-wide (not per circuit, like
    /// this scoped service's other state) because it's a property of the server this instance talks
    /// to: the fallback then costs one wasted round trip in total rather than one per patient.
    /// </summary>
    private static volatile bool _encounterDateSortUnsupported;

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
        List<Resource> resources;
        try
        {
            resources = await FetchEncountersWithLocationsAsync(patientId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch encounters for Patient/{PatientId}", patientId);
            return null;
        }

        // Deduplicated rather than handed straight to ToDictionary: a FHIR server re-runs
        // _include=Encounter:location for every page it emits, so the paged fallback below sees the
        // same Location id once per page it appears on, and ToDictionary would throw on the repeat.
        var locationsById = resources
            .OfType<Location>()
            .DistinctBy(l => l.Id)
            .ToDictionary(l => $"Location/{l.Id}", l => l);

        // Parsed rather than compared as strings: without a server-side sort, being newest-first is
        // this method's whole correctness, and FHIR instants vary in offset and precision.
        var encounters = resources
            .OfType<Encounter>()
            .OrderByDescending(e => FhirBundleHelpers.ParseFhirInstant(e.Period?.Start))
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
    /// The patient's encounters plus their _include'd Locations - newest-first when the server can
    /// sort them for us, unordered when it can't (see <see cref="_encounterDateSortUnsupported"/>);
    /// either way GetLatestLocationAsync sorts what comes back.
    /// </summary>
    private async Task<List<Resource>> FetchEncountersWithLocationsAsync(
        string patientId,
        CancellationToken ct
    )
    {
        var query =
            $"Encounter?subject=Patient/{patientId}&_include=Encounter:location&_pretty=false";

        if (!_encounterDateSortUnsupported)
        {
            try
            {
                // Deliberately a single-page fetch, not FhirBundleHelpers.GetAllPagesAsync:
                // _count=5 + _sort=-date already gives us the only encounters we could ever use,
                // since we return on the first one with a usable location.
                var bundle =
                    await clientFactory.CreateClient().GetAsync($"{query}&_count=5&_sort=-date", ct)
                    as Bundle;

                return bundle
                        ?.Entry.Where(e => e.Resource is not null)
                        .Select(e => e.Resource!)
                        .ToList()
                    ?? [];
            }
            catch (FhirOperationException ex) when (ex.Status == HttpStatusCode.BadRequest)
            {
                logger.LogInformation(
                    ex,
                    "FHIR server rejected '_sort=-date' on Encounter search - falling back to fetching each patient's encounters unsorted and ordering them client-side"
                );
                _encounterDateSortUnsupported = true;
            }
        }

        // Unsorted, "the newest 5" means nothing - the server may return any five of the patient's
        // encounters - so the whole history has to come back for the ordering above to find the
        // latest one.
        return await FhirBundleHelpers.GetAllPagesAsync(
            clientFactory.CreateClient(),
            $"{query}&_count=100",
            ct
        );
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
