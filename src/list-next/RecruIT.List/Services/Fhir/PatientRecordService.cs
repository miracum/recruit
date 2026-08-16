using Hl7.Fhir.Model;
using RecruIT.List.Models;
using RecruIT.List.Resources;
using Microsoft.Extensions.Localization;

namespace RecruIT.List.Services.Fhir;

public sealed class PatientClinicalSummaryDto
{
    public Patient? Patient { get; init; }

    public IReadOnlyList<Condition> Conditions { get; init; } = [];

    public IReadOnlyList<Procedure> Procedures { get; init; } = [];

    public IReadOnlyList<MedicationStatement> MedicationStatements { get; init; } = [];

    public IReadOnlyList<MedicationAdministration> MedicationAdministrations { get; init; } = [];

    public IReadOnlyList<Observation> Observations { get; init; } = [];
}

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
    public async Task<PatientClinicalSummaryDto> GetClinicalSummaryAsync(
        string patientId,
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(
                client,
                $"Patient/{patientId}/$everything?_count=250&_pretty=false",
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch $everything for Patient/{PatientId}", patientId);
            throw new FhirAccessException(localizer["App.Errors.ClinicalRecordLoadFailed"], ex);
        }

        return new PatientClinicalSummaryDto
        {
            Patient = resources.OfType<Patient>().FirstOrDefault(),
            Conditions = resources.OfType<Condition>().ToList(),
            Procedures = resources.OfType<Procedure>().ToList(),
            MedicationStatements = resources.OfType<MedicationStatement>().ToList(),
            MedicationAdministrations = resources.OfType<MedicationAdministration>().ToList(),
            Observations = resources.OfType<Observation>().ToList(),
        };
    }

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

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(
                client,
                $"Encounter?subject=Patient/{patientId}&_count=5&_include=Encounter:location&_sort=-date&_pretty=false",
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch encounters for Patient/{PatientId}", patientId);
            return null;
        }

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

            if (
                locationEntry?.Location?.Reference is { } reference
                && locationsById.TryGetValue(reference, out var location)
            )
            {
                return new LatestLocationDto
                {
                    LocationName = location.Name ?? localizer["App.Common.Unknown"],
                    EncounterStart = ParseInstant(encounter.Period?.Start),
                };
            }

            if (!string.IsNullOrEmpty(locationEntry?.Location?.Display))
            {
                return new LatestLocationDto
                {
                    LocationName = locationEntry.Location.Display,
                    EncounterStart = ParseInstant(encounter.Period?.Start),
                };
            }

            if (!string.IsNullOrEmpty(encounter.ServiceProvider?.Display))
            {
                return new LatestLocationDto
                {
                    LocationName = encounter.ServiceProvider.Display,
                    EncounterStart = ParseInstant(encounter.Period?.Start),
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
            + $"&focus:ResearchStudy.identifier={Uri.EscapeDataString($"{trialIdentifier.System}|{trialIdentifier.Value}")}"
            + $"&category={Uri.EscapeDataString($"{RecruIT.List.Services.Fhir.FhirConstants.SystemObservationCategory}|{RecruIT.List.Services.Fhir.FhirConstants.ObservationCategoryEligibilityAssessment}")}";

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
            RecruIT.List.Services.Fhir.FhirConstants.SnomedCodeYes => (true, false),
            RecruIT.List.Services.Fhir.FhirConstants.SnomedCodeNo => (false, false),
            RecruIT.List.Services.Fhir.FhirConstants.SnomedCodeIndeterminate => ((bool?)null, true),
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

    private static DateTimeOffset? ParseInstant(string? value) =>
        value is not null && DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}
