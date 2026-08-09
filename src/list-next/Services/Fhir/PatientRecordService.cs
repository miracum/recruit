using Hl7.Fhir.Model;
using list.Resources;
using Microsoft.Extensions.Localization;

namespace list.Services.Fhir;

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
    ILogger<PatientRecordService> logger)
{
    public async Task<PatientClinicalSummaryDto> GetClinicalSummaryAsync(string patientId, CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(
                client, $"Patient/{patientId}/$everything?_count=250&_pretty=false", ct).ConfigureAwait(false);
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
    public async Task<LatestLocationDto?> GetLatestLocationAsync(string patientId, CancellationToken ct = default)
    {
        var client = clientFactory.CreateClient();

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(
                client,
                $"Encounter?subject=Patient/{patientId}&_count=5&_include=Encounter:location&_sort=-date&_pretty=false",
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch encounters for Patient/{PatientId}", patientId);
            return null;
        }

        var locationsById = resources.OfType<Location>().ToDictionary(l => $"Location/{l.Id}", l => l);
        var encounters = resources.OfType<Encounter>()
            .OrderByDescending(e => e.Period?.Start)
            .ToList();

        foreach (var encounter in encounters)
        {
            var locationEntry = encounter.Location?
                .OrderByDescending(l => l.Period?.Start)
                .FirstOrDefault();

            if (locationEntry?.Location?.Reference is { } reference && locationsById.TryGetValue(reference, out var location))
            {
                return new LatestLocationDto { LocationName = location.Name ?? localizer["App.Common.Unknown"], EncounterStart = ParseInstant(encounter.Period?.Start) };
            }

            if (!string.IsNullOrEmpty(locationEntry?.Location?.Display))
            {
                return new LatestLocationDto { LocationName = locationEntry.Location.Display, EncounterStart = ParseInstant(encounter.Period?.Start) };
            }

            if (!string.IsNullOrEmpty(encounter.ServiceProvider?.Display))
            {
                return new LatestLocationDto { LocationName = encounter.ServiceProvider.Display, EncounterStart = ParseInstant(encounter.Period?.Start) };
            }
        }

        return null;
    }

    private static DateTimeOffset? ParseInstant(string? value) =>
        value is not null && DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}
