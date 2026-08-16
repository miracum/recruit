using System.Text;
using De.Medizininformatikinitiative.Kerndatensatz.Studie;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Localization;
using RecruIT.List.Models;
using RecruIT.List.Resources;

namespace RecruIT.List.Services.Fhir;

/// <summary>
/// Backs the admin-only Criteria Manager page: lets an admin browse existing ResearchStudy and
/// Library resources, and assemble the FHIR resources (ResearchStudy, Group, one Library per
/// criterion) that a study's eligibility criteria would consist of, per
/// docs/trino/eligibility-criteria-design.md. The authoring side is not persisted - actually
/// creating these resources against the FHIR server is a separate, not-yet-built step; that part
/// only builds and serializes a preview so an admin can inspect/copy the result.
/// </summary>
public sealed class EligibilityCriteriaService(
    FhirClientFactory clientFactory,
    IStringLocalizer<SharedResources> localizer,
    ILogger<EligibilityCriteriaService> logger
)
{
    /// <summary>
    /// Library.type coding that marks a Library as SQL-on-FHIR eligibility-criterion SQL, as
    /// opposed to any other kind of Library resource a FHIR server might hold - see
    /// query-sql-on-fhir's PollForStudies.SQL_QUERY_LIBRARY_TYPE_SYSTEM/_CODE, which writes this
    /// same coding.
    /// </summary>
    private const string SqlQueryLibraryTypeSystem =
        "https://sql-on-fhir.org/ig/CodeSystem/LibraryTypesCodes";
    private const string SqlQueryLibraryTypeCode = "sql-query";

    public async Task<IReadOnlyList<ResearchStudySummaryDto>> SearchResearchStudiesAsync(
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();
        var query = "ResearchStudy?_count=50&_sort=-_lastUpdated";

        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(client, query, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search ResearchStudy resources");
            throw new FhirAccessException(localizer["App.Errors.ResearchStudiesLoadFailed"], ex);
        }

        return resources
            .OfType<ResearchStudy>()
            .Where(s => s.Id is not null)
            .Select(s => new ResearchStudySummaryDto(
                s.Id!,
                s.Title,
                s.GetMiiExStudieAkronym()?.Value?.ToString(),
                s.GetTrialIdentifier()
            ))
            .OrderBy(s => s.Acronym ?? s.Title ?? s.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Lists every Library resource on the FHIR server (the eligibility-criterion catalog), each
    /// with its decoded SQL and the display names of any ResearchStudy(ies) currently referencing
    /// it (via that study's enrolled Group.characteristic) - resolved by fetching all Group and
    /// ResearchStudy resources and joining in memory, same "fetch + aggregate in C#" convention
    /// used elsewhere in this app. Fine at this app's scale; would need real search-parameter
    /// support (chained search) to stay cheap at a much larger resource count.
    /// </summary>
    public async Task<IReadOnlyList<LibrarySummaryDto>> SearchLibrariesAsync(
        CancellationToken ct = default
    )
    {
        var client = clientFactory.CreateClient();

        List<Resource> libraries;
        List<Resource> groups;
        List<Resource> studies;
        try
        {
            libraries = await FhirBundleHelpers.GetAllPagesAsync(
                client,
                $"Library?type={Uri.EscapeDataString($"{SqlQueryLibraryTypeSystem}|{SqlQueryLibraryTypeCode}")}"
                    + "&_count=100&_sort=-_lastUpdated",
                ct
            );
            groups = await FhirBundleHelpers.GetAllPagesAsync(client, "Group?_count=100", ct);
            studies = await FhirBundleHelpers.GetAllPagesAsync(
                client,
                "ResearchStudy?_count=100",
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search Library resources");
            throw new FhirAccessException(localizer["App.Errors.LibrariesLoadFailed"], ex);
        }

        var studiesByGroupId = studies
            .OfType<ResearchStudy>()
            .SelectMany(s => s.Enrollment.Select(e => (Study: s, GroupId: e.GetReferencedId())))
            .Where(x => x.GroupId is not null)
            .ToLookup(x => x.GroupId!, x => x.Study);

        var studyNamesByLibraryId = groups
            .OfType<Group>()
            .Where(g => g.Id is not null)
            .SelectMany(g =>
                g.Characteristic.Select(c => (c.Value as ResourceReference).GetReferencedId())
                    .Where(libraryId => libraryId is not null)
                    .SelectMany(libraryId =>
                        studiesByGroupId[g.Id!]
                            .Select(study => (LibraryId: libraryId!, Study: study))
                    )
            )
            .ToLookup(x => x.LibraryId, x => StudyDisplayName(x.Study));

        return libraries
            .OfType<Library>()
            .Where(l => l.Id is not null)
            .Select(l => new LibrarySummaryDto(
                l.Id!,
                l.Title,
                l.Name,
                l.Description,
                l.Status?.ToString(),
                l.GetSqlText(),
                l.Meta?.LastUpdated,
                [
                    .. studyNamesByLibraryId[l.Id!]
                        .Distinct()
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
                ]
            ))
            .OrderBy(l => l.Title ?? l.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string StudyDisplayName(ResearchStudy study) =>
        study.GetStudyAcronym() is { Length: > 0 } acronym
            ? acronym
            : study.Title ?? study.Id ?? "?";

    /// <summary>
    /// Assembles the draft into a preview-only FHIR transaction Bundle and serializes it to
    /// pretty-printed JSON. Bundle shape: one Library entry per newly-authored criterion, one Group
    /// entry referencing all of them (plus any reused-by-reference criteria) via
    /// Group.characteristic, and one ResearchStudy entry enrolling that Group.
    /// </summary>
    public string BuildPreviewJson(
        ResearchStudyDraft studyDraft,
        IReadOnlyList<CriterionDraft> criteria
    )
    {
        var bundle = BuildPreviewBundle(studyDraft, criteria);
        return new FhirJsonSerializer().SerializeToString(bundle, pretty: true);
    }

    private static Bundle BuildPreviewBundle(
        ResearchStudyDraft studyDraft,
        IReadOnlyList<CriterionDraft> criteria
    )
    {
        var libraryEntries = new List<Bundle.EntryComponent>();
        var characteristics = new List<Group.CharacteristicComponent>();

        foreach (var criterion in criteria)
        {
            ResourceReference libraryReference;

            if (!string.IsNullOrWhiteSpace(criterion.ExistingLibraryReference))
            {
                libraryReference = new ResourceReference(criterion.ExistingLibraryReference.Trim());
            }
            else
            {
                var libraryFullUrl = "urn:uuid:" + Guid.NewGuid();
                var library = new Library
                {
                    Status = PublicationStatus.Active,
                    Title = criterion.DisplayText,
                    Content =
                    [
                        new Attachment
                        {
                            ContentType = "application/sql",
                            Data = Encoding.UTF8.GetBytes(criterion.Sql),
                        },
                    ],
                };

                libraryEntries.Add(
                    new Bundle.EntryComponent
                    {
                        FullUrl = libraryFullUrl,
                        Resource = library,
                        Request = new Bundle.RequestComponent
                        {
                            Method = Bundle.HTTPVerb.POST,
                            Url = "Library",
                        },
                    }
                );

                libraryReference = new ResourceReference(libraryFullUrl);
            }

            characteristics.Add(
                new Group.CharacteristicComponent
                {
                    Code = new CodeableConcept(
                        system: null,
                        code: null,
                        text: criterion.DisplayText
                    ),
                    Value = libraryReference,
                    Exclude = criterion.Exclude,
                }
            );
        }

        var groupFullUrl = "urn:uuid:" + Guid.NewGuid();
        var group = new Group
        {
            Type = Group.GroupType.Person,
            Actual = true,
            Characteristic = characteristics,
        };

        var study = new ResearchStudy
        {
            Status = ResearchStudy.ResearchStudyStatus.Active,
            Title = studyDraft.Title,
            Enrollment = [new ResourceReference(groupFullUrl)],
        };

        if (!string.IsNullOrWhiteSpace(studyDraft.Acronym))
        {
            study.Extension.Add(
                Studie.Extensions.MiiExStudieAkronym(new FhirString(studyDraft.Acronym))
            );
        }

        if (
            !string.IsNullOrWhiteSpace(studyDraft.IdentifierSystem)
            && !string.IsNullOrWhiteSpace(studyDraft.IdentifierValue)
        )
        {
            study.Identifier.Add(
                new Identifier(studyDraft.IdentifierSystem, studyDraft.IdentifierValue)
            );
        }

        var studyEntry = new Bundle.EntryComponent { Resource = study };
        if (!string.IsNullOrWhiteSpace(studyDraft.ExistingId))
        {
            study.Id = studyDraft.ExistingId;
            studyEntry.Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.PUT,
                Url = $"ResearchStudy/{studyDraft.ExistingId}",
            };
        }
        else
        {
            studyEntry.Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.POST,
                Url = "ResearchStudy",
            };
        }

        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };
        bundle.Entry.Add(studyEntry);
        bundle.Entry.Add(
            new Bundle.EntryComponent
            {
                FullUrl = groupFullUrl,
                Resource = group,
                Request = new Bundle.RequestComponent
                {
                    Method = Bundle.HTTPVerb.POST,
                    Url = "Group",
                },
            }
        );
        bundle.Entry.AddRange(libraryEntries);

        return bundle;
    }
}
