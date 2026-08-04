using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Recruit.List.Web.Fhir.Models;

namespace Recruit.List.Web.Fhir;

public sealed class FhirService : IFhirService
{
    // keep in sync with the extension URL previously used by src/list/frontend/src/const.js
    private const string NoteExtensionUrl =
        "https://fhir.miracum.org/uc1/StructureDefinition/researchSubjectNote";
    private const string StudyAcronymExtensionUrl =
        "https://fhir.miracum.org/uc1/StructureDefinition/studyAcronym";
    private const string IdentifierTypeSystem = "http://terminology.hl7.org/CodeSystem/v2-0203";
    private const string MrIdentifierTypeCode = "MR";

    // several hundred ResearchSubjects should comfortably fit on a single page; the loop below
    // still follows "next" links in case the server paginates anyway, bounded so a misbehaving
    // server can't make this loop forever.
    private const int PageSize = 200;
    private const int MaxPages = 25;

    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<FhirService> _logger;

    public FhirService(
        HttpClient httpClient,
        TokenProvider tokenProvider,
        ILogger<FhirService> logger
    )
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResearchSubjectRow>> GetResearchSubjectsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var patients = new Dictionary<string, FhirPatient>(StringComparer.Ordinal);
        var studies = new Dictionary<string, FhirResearchStudy>(StringComparer.Ordinal);
        var subjects = new List<FhirResearchSubject>();

        string? nextUrl =
            $"ResearchSubject?_count={PageSize}&_include=ResearchSubject:individual&_include=ResearchSubject:study&_sort=-_lastUpdated";

        for (var page = 0; nextUrl is not null && page < MaxPages; page++)
        {
            var bundle = await GetBundleAsync(nextUrl, cancellationToken);

            foreach (var entry in bundle.Entry ?? [])
            {
                if (!entry.Resource.TryGetProperty("resourceType", out var resourceTypeElement))
                {
                    continue;
                }

                switch (resourceTypeElement.GetString())
                {
                    case "Patient":
                        var patient = entry.Resource.Deserialize<FhirPatient>();
                        if (patient?.Id is not null)
                        {
                            patients[patient.Id] = patient;
                        }

                        break;
                    case "ResearchSubject":
                        var subject = entry.Resource.Deserialize<FhirResearchSubject>();
                        if (subject is not null)
                        {
                            subjects.Add(subject);
                        }

                        break;
                    case "ResearchStudy":
                        var study = entry.Resource.Deserialize<FhirResearchStudy>();
                        if (study?.Id is not null)
                        {
                            studies[study.Id] = study;
                        }

                        break;
                }
            }

            nextUrl = bundle.Link?.FirstOrDefault(link => link.Relation == "next")?.Url;
        }

        _logger.LogDebug(
            "Fetched {SubjectCount} ResearchSubjects, {PatientCount} Patients, and {StudyCount} ResearchStudies",
            subjects.Count,
            patients.Count,
            studies.Count
        );

        return subjects
            .Select(subject => ToRow(subject, patients, studies))
            .OfType<ResearchSubjectRow>()
            .ToList();
    }

    public async Task UpdateResearchSubjectAsync(
        string researchSubjectId,
        string status,
        string? note,
        CancellationToken cancellationToken = default
    )
    {
        var patch = new List<object>
        {
            new
            {
                op = "replace",
                path = "/status",
                value = status,
            },
        };

        if (note is not null)
        {
            patch.Add(
                new
                {
                    op = "add",
                    path = "/extension",
                    value = new[] { new { url = NoteExtensionUrl, valueString = note } },
                }
            );
        }

        using var request = CreateRequest(HttpMethod.Patch, $"ResearchSubject/{researchSubjectId}");
        request.Content = JsonContent.Create(
            patch,
            mediaType: new MediaTypeHeaderValue("application/json-patch+json")
        );

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<FhirBundle> GetBundleAsync(
        string urlOrPath,
        CancellationToken cancellationToken
    )
    {
        using var request = CreateRequest(HttpMethod.Get, urlOrPath);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<FhirBundle>(
                cancellationToken: cancellationToken
            )
            ?? throw new FhirRequestException(
                (int)response.StatusCode,
                "The FHIR server returned an empty response body."
            );
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string urlOrPath)
    {
        var request = new HttpRequestMessage(
            method,
            new Uri(urlOrPath, UriKind.RelativeOrAbsolute)
        );
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));

        if (!string.IsNullOrEmpty(_tokenProvider.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _tokenProvider.AccessToken
            );
        }

        return request;
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = response.ReasonPhrase ?? "unknown error";

        try
        {
            var outcome = await response.Content.ReadFromJsonAsync<FhirOperationOutcome>(
                cancellationToken: cancellationToken
            );
            var diagnostics = outcome
                ?.Issue?.Select(issue => issue.Diagnostics)
                .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
                .ToList();

            if (diagnostics is { Count: > 0 })
            {
                detail = string.Join("; ", diagnostics);
            }
        }
        catch (JsonException)
        {
            // the error response wasn't a FHIR OperationOutcome; fall back to the reason phrase above.
        }

        _logger.LogWarning(
            "FHIR request failed with status {StatusCode}: {Detail}",
            (int)response.StatusCode,
            detail
        );
        throw new FhirRequestException((int)response.StatusCode, detail);
    }

    private static ResearchSubjectRow? ToRow(
        FhirResearchSubject subject,
        Dictionary<string, FhirPatient> patients,
        Dictionary<string, FhirResearchStudy> studies
    )
    {
        if (subject.Id is null || subject.Individual?.Reference is null)
        {
            return null;
        }

        var patientId = ExtractId(subject.Individual.Reference);
        var patient =
            patientId is not null && patients.TryGetValue(patientId, out var foundPatient)
                ? foundPatient
                : null;

        var studyId = subject.Study?.Reference is not null
            ? ExtractId(subject.Study.Reference)
            : null;
        var study =
            studyId is not null && studies.TryGetValue(studyId, out var foundStudy)
                ? foundStudy
                : null;

        var mrNumber = patient
            ?.Identifier?.FirstOrDefault(identifier =>
                identifier.Type?.Coding?.Any(coding =>
                    coding.System == IdentifierTypeSystem && coding.Code == MrIdentifierTypeCode
                ) == true
            )
            ?.Value;

        var note = subject
            .Extension?.FirstOrDefault(extension => extension.Url == NoteExtensionUrl)
            ?.ValueString;

        var studyDisplay =
            study
                ?.Extension?.FirstOrDefault(extension => extension.Url == StudyAcronymExtensionUrl)
                ?.ValueString
            ?? study?.Title
            ?? subject.Study?.Display;

        int? birthYear = null;
        if (
            patient?.BirthDate is not null
            && DateTime.TryParse(
                patient.BirthDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var birthDate
            )
        )
        {
            birthYear = birthDate.Year;
        }

        return new ResearchSubjectRow
        {
            ResearchSubjectId = subject.Id,
            PatientId = patientId ?? subject.Individual.Reference,
            Status = subject.Status ?? "candidate",
            Note = note,
            MrNumber = mrNumber,
            BirthYear = birthYear,
            Gender = patient?.Gender,
            StudyDisplay = studyDisplay,
            EnrolledSince = subject.Period?.Start ?? subject.Meta?.LastUpdated,
        };
    }

    private static string? ExtractId(string reference) =>
        reference.Split('/') is { Length: > 0 } parts ? parts[^1] : null;
}
