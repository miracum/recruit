using System.Net;
using System.Net.Http.Headers;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using FhirList = Hl7.Fhir.Model.List;
using ListFhirConstants = RecruIT.List.Services.Fhir.FhirConstants;
using Task = System.Threading.Tasks.Task;

namespace RecruIT.List.E2ETests;

/// <summary>
/// Loads a fresh FHIR server with what a real deployment has: recruIT's SearchParameters and the
/// sample study, the same bundles the chart and compose load, plus a small screening list for it -
/// the part the query module would otherwise produce. Built with the generated FhirConstants
/// rather than as a JSON file, so the codes and URLs can't drift from what the app looks for.
/// </summary>
internal static class FhirSeed
{
    /// <summary>The ResearchStudy in charts/recruit/files/fhir/sample-study-transaction-upsert.json.</summary>
    private const string SampleStudyId = "98d96608-475f-4386-b7b5-6ddc1eb57e71";

    private static readonly (string Given, string Family, AdministrativeGender Gender)[] Patients =
    [
        ("Erika", "Mustermann", AdministrativeGender.Female),
        ("Max", "Mustermann", AdministrativeGender.Male),
    ];

    public static async Task SeedAsync(string fhirBaseUrl, TimeSpan timeout)
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(fhirBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };

        // SearchParameters first: resources are only indexed for the ones that already exist when
        // they're written, and the app's List:belongs-to-study include depends on it.
        await PostBundleAsync(http, "search-parameters-transaction-upsert.json");
        await WaitForSearchParameterAsync(http, timeout);

        await PostBundleAsync(http, "sample-study-transaction-upsert.json");
        var client = new FhirClient(fhirBaseUrl);
        await client.TransactionAsync(BuildScreeningListBundle());
    }

    private static async Task PostBundleAsync(HttpClient http, string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fhir", fileName);
        using var content = new StringContent(await File.ReadAllTextAsync(path));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/fhir+json");
        using var response = await http.PostAsync("", content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// HAPI picks up new SearchParameters with a short delay, and rejects searches on one it
    /// doesn't know yet with a 400 - so this waits until belongs-to-study is searchable.
    /// </summary>
    private static async Task WaitForSearchParameterAsync(HttpClient http, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            using var response = await http.GetAsync(
                $"List?belongs-to-study=ResearchStudy/{SampleStudyId}"
            );
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            if (response.StatusCode != HttpStatusCode.BadRequest || DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"The belongs-to-study SearchParameter wasn't searchable within {timeout} (last status {response.StatusCode})."
                );
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
    }

    private static Bundle BuildScreeningListBundle()
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };
        var study = new ResourceReference($"ResearchStudy/{SampleStudyId}");
        var list = new FhirList
        {
            Id = "e2e-screening-list",
            Status = FhirList.ListStatus.Current,
            Mode = ListMode.Working,
            Code = new CodeableConcept(
                ListFhirConstants.SystemScreeningList,
                ListFhirConstants.ScreeningListCode
            ),
        };
        list.Extension.Add(new Extension(ListFhirConstants.UrlListBelongsToStudy, study));

        for (var i = 0; i < Patients.Length; i++)
        {
            var patientId = $"e2e-patient-{i + 1}";
            var subjectId = $"e2e-subject-{i + 1}";
            AddPut(
                bundle,
                new Patient
                {
                    Id = patientId,
                    Name =
                    [
                        new HumanName { Family = Patients[i].Family, Given = [Patients[i].Given] },
                    ],
                    Gender = Patients[i].Gender,
                    BirthDate = "1970-01-01",
                }
            );
            AddPut(
                bundle,
                new ResearchSubject
                {
                    Id = subjectId,
                    Status = ResearchSubject.ResearchSubjectStatus.Candidate,
                    Study = study,
                    Individual = new ResourceReference($"Patient/{patientId}"),
                    Identifier =
                    [
                        new Identifier(ListFhirConstants.UrlResearchSubjectIdentifier, subjectId),
                    ],
                }
            );
            list.Entry.Add(
                new FhirList.EntryComponent
                {
                    Item = new ResourceReference($"ResearchSubject/{subjectId}"),
                    DateElement = new FhirDateTime(DateTimeOffset.UtcNow.AddDays(-(i + 1))),
                }
            );
        }

        AddPut(bundle, list);
        return bundle;
    }

    private static void AddPut(Bundle bundle, Resource resource) =>
        bundle.Entry.Add(
            new Bundle.EntryComponent
            {
                Resource = resource,
                Request = new Bundle.RequestComponent
                {
                    Method = Bundle.HTTPVerb.PUT,
                    Url = $"{resource.TypeName}/{resource.Id}",
                },
            }
        );
}
