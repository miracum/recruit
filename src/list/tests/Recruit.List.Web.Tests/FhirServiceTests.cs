using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Recruit.List.Web.Fhir;
using Xunit;

namespace Recruit.List.Web.Tests;

public class FhirServiceTests
{
    private static readonly string SearchSetFixturePath = Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "research-subject-searchset.json"
    );

    private static (
        FhirService Service,
        StubHttpMessageHandler Handler,
        TokenProvider TokenProvider
    ) CreateService()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://fhir.example.test/fhir/"),
        };
        var tokenProvider = new TokenProvider();
        var service = new FhirService(httpClient, tokenProvider, NullLogger<FhirService>.Instance);
        return (service, handler, tokenProvider);
    }

    [Fact]
    public async Task GetResearchSubjectsAsync_JoinsPatientAndStudyDataOntoEachResearchSubject()
    {
        var (service, handler, _) = CreateService();
        handler.Enqueue(HttpStatusCode.OK, File.ReadAllText(SearchSetFixturePath));

        var rows = await service.GetResearchSubjectsAsync();

        Assert.Equal(3, rows.Count);

        var onStudyRow = Assert.Single(rows, row => row.ResearchSubjectId == "1");
        Assert.Equal("on-study", onStudyRow.Status);
        Assert.Equal("PROSa", onStudyRow.StudyDisplay);
        Assert.Equal(1980, onStudyRow.BirthYear);
        Assert.Equal("female", onStudyRow.Gender);
        Assert.Equal("MRN-0001", onStudyRow.MrNumber);
        Assert.Equal("MRN-0001", onStudyRow.DisplayId);
        Assert.Equal("called the patient back", onStudyRow.Note);
        Assert.Equal(
            new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
            onStudyRow.EnrolledSince
        );

        var candidateRow = Assert.Single(rows, row => row.ResearchSubjectId == "2");
        // patient-2 has no MR-type identifier, so the row must fall back to the raw Patient id.
        Assert.Null(candidateRow.MrNumber);
        Assert.Equal("patient-2", candidateRow.DisplayId);
        Assert.Equal(1965, candidateRow.BirthYear);
        // study-2 has no acronym extension, so the display falls back to its title.
        var withdrawnRow = Assert.Single(rows, row => row.ResearchSubjectId == "3");
        Assert.Equal("AMICA study without an acronym extension", withdrawnRow.StudyDisplay);
    }

    [Fact]
    public async Task GetResearchSubjectsAsync_SkipsResearchSubjectsWithoutAnIndividualReference()
    {
        var (service, handler, _) = CreateService();
        handler.Enqueue(
            HttpStatusCode.OK,
            """
            {
              "resourceType": "Bundle",
              "type": "searchset",
              "entry": [
                { "resource": { "resourceType": "ResearchSubject", "id": "orphan", "status": "candidate" } }
              ]
            }
            """
        );

        var rows = await service.GetResearchSubjectsAsync();

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetResearchSubjectsAsync_FollowsPaginationLinks()
    {
        var (service, handler, _) = CreateService();
        handler.Enqueue(
            HttpStatusCode.OK,
            """
            {
              "resourceType": "Bundle",
              "type": "searchset",
              "link": [{ "relation": "next", "url": "http://fhir.example.test/fhir/ResearchSubject?page=2" }],
              "entry": [
                {
                  "resource": {
                    "resourceType": "ResearchSubject",
                    "id": "subject-1",
                    "status": "candidate",
                    "individual": { "reference": "Patient/patient-1" }
                  }
                }
              ]
            }
            """
        );
        handler.Enqueue(
            HttpStatusCode.OK,
            """
            {
              "resourceType": "Bundle",
              "type": "searchset",
              "entry": [
                {
                  "resource": {
                    "resourceType": "ResearchSubject",
                    "id": "subject-2",
                    "status": "on-study",
                    "individual": { "reference": "Patient/patient-2" }
                  }
                }
              ]
            }
            """
        );

        var rows = await service.GetResearchSubjectsAsync();

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(["subject-1", "subject-2"], rows.Select(row => row.ResearchSubjectId).Order());
    }

    [Fact]
    public async Task GetResearchSubjectsAsync_AttachesBearerTokenWhenAvailable()
    {
        var (service, handler, tokenProvider) = CreateService();
        tokenProvider.AccessToken = "the-access-token";
        handler.Enqueue(HttpStatusCode.OK, File.ReadAllText(SearchSetFixturePath));

        await service.GetResearchSubjectsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("the-access-token", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetResearchSubjectsAsync_ThrowsWithDiagnosticsFromOperationOutcome()
    {
        var (service, handler, _) = CreateService();
        handler.Enqueue(
            HttpStatusCode.Forbidden,
            """
            {
              "resourceType": "OperationOutcome",
              "issue": [{ "severity": "error", "code": "forbidden", "diagnostics": "access denied for this study" }]
            }
            """
        );

        var exception = await Assert.ThrowsAsync<FhirRequestException>(() =>
            service.GetResearchSubjectsAsync()
        );

        Assert.Equal(403, exception.StatusCode);
        Assert.Contains("access denied for this study", exception.Message);
    }

    [Fact]
    public async Task UpdateResearchSubjectAsync_SendsAJsonPatchRequestWithStatusAndNote()
    {
        var (service, handler, _) = CreateService();
        handler.Enqueue(HttpStatusCode.OK, "{}");

        await service.UpdateResearchSubjectAsync(
            "subject-1",
            "on-study",
            "called the patient back"
        );

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal(
            "http://fhir.example.test/fhir/ResearchSubject/subject-1",
            request.RequestUri!.ToString()
        );
        Assert.Equal(
            "application/json-patch+json",
            request.Content!.Headers.ContentType!.MediaType
        );

        using var patch = JsonDocument.Parse(Assert.Single(handler.RequestBodies)!);
        var operations = patch.RootElement;

        Assert.Equal("replace", operations[0].GetProperty("op").GetString());
        Assert.Equal("/status", operations[0].GetProperty("path").GetString());
        Assert.Equal("on-study", operations[0].GetProperty("value").GetString());

        Assert.Equal("/extension", operations[1].GetProperty("path").GetString());
        Assert.Equal(
            "called the patient back",
            operations[1].GetProperty("value")[0].GetProperty("valueString").GetString()
        );
    }

    [Fact]
    public async Task UpdateResearchSubjectAsync_OmitsTheNotePatchWhenNoteIsNull()
    {
        var (service, handler, _) = CreateService();
        handler.Enqueue(HttpStatusCode.OK, "{}");

        await service.UpdateResearchSubjectAsync("subject-1", "withdrawn", note: null);

        using var patch = JsonDocument.Parse(Assert.Single(handler.RequestBodies)!);

        Assert.Equal(1, patch.RootElement.GetArrayLength());
    }
}
