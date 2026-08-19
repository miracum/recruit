using System.Security.Cryptography;
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

    /// <summary>
    /// SQL-on-FHIR's own extension for carrying the SQL text alongside content.data's base64 - see
    /// https://build.fhir.org/ig/FHIR/sql-on-fhir-v2/StructureDefinition-SQLQuery.html#sql-attachments.
    /// </summary>
    private const string SqlTextExtensionUrl =
        "https://sql-on-fhir.org/ig/StructureDefinition/sql-text";

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
                // Library.name is always the slugified Title, never independently authored - so its
                // identity (and thus the update-as-create hash below) is purely a function of Title.
                // Two ad hoc criteria in the same study sharing a Title collide into one Library by
                // design, same as the "point a second study's characteristic at an existing Library"
                // dedup story in docs/trino/eligibility-criteria-design.md.
                var name = Slug.Create(criterion.DisplayText);
                var identifierValue = Slug.Create(name);
                var libraryId = ComputeIdentifierHashId(
                    FhirConstants.UrlUiCreatedEligibilityLibraryIdentifier,
                    identifierValue
                );

                var library = new Library
                {
                    Id = libraryId,
                    Status = PublicationStatus.Active,
                    Title = criterion.DisplayText,
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(criterion.Description)
                        ? null
                        : criterion.Description,
                    // Must match the type SearchLibrariesAsync filters on and PollForStudies'
                    // chained discovery search looks for - without it this Library would be
                    // invisible to both the catalog and the poll job once actually persisted.
                    Type = new CodeableConcept(SqlQueryLibraryTypeSystem, SqlQueryLibraryTypeCode),
                    Content =
                    [
                        new Attachment
                        {
                            ContentType = "application/sql",
                            Extension =
                            [
                                new Extension(SqlTextExtensionUrl, new FhirString(criterion.Sql)),
                            ],
                            Data = Encoding.UTF8.GetBytes(criterion.Sql),
                        },
                    ],
                };
                library.Identifier.Add(
                    new Identifier(
                        FhirConstants.UrlUiCreatedEligibilityLibraryIdentifier,
                        identifierValue
                    )
                );

                libraryEntries.Add(
                    new Bundle.EntryComponent
                    {
                        Resource = library,
                        Request = new Bundle.RequestComponent
                        {
                            Method = Bundle.HTTPVerb.PUT,
                            Url = $"Library/{libraryId}",
                        },
                    }
                );

                libraryReference = new ResourceReference($"Library/{libraryId}");
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

        var group = new Group
        {
            Type = Group.GroupType.Person,
            Actual = false,
            Active = true,
            Characteristic = characteristics,
        };

        // A UI-assigned, title-derived identifier shared by the Group and ResearchStudy below (each
        // under their own resource-specific system, so they still hash to different ids) - same
        // story as the ad hoc Library criteria above, which only ever have
        // FhirConstants.UrlUiCreatedEligibilityLibraryIdentifier to go on. Always computable (Title
        // is never null), so both always get a deterministic update-as-create id, never a plain POST
        // with a server-assigned one. There's currently no way to author against an existing
        // ResearchStudy through this page - every submission creates or replaces the one this
        // identifier hashes to.
        var uiCreatedIdentifierValue = Slug.Create(studyDraft.Title);

        group.Identifier.Add(
            new Identifier(FhirConstants.UrlEligibilityGroupIdentifier, uiCreatedIdentifierValue)
        );
        var groupId = ComputeIdentifierHashId(
            FhirConstants.UrlEligibilityGroupIdentifier,
            uiCreatedIdentifierValue
        );
        group.Id = groupId;
        var groupReference = new ResourceReference($"Group/{groupId}");
        var groupEntry = new Bundle.EntryComponent
        {
            Resource = group,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.PUT,
                Url = $"Group/{groupId}",
            },
        };

        var study = new ResearchStudy
        {
            Status = ResearchStudy.ResearchStudyStatus.Active,
            Title = studyDraft.Title,
            Description = string.IsNullOrWhiteSpace(studyDraft.Description)
                ? null
                : studyDraft.Description,
            Enrollment = [groupReference],
        };

        if (!string.IsNullOrWhiteSpace(studyDraft.Acronym))
        {
            study.Extension.Add(
                Studie.Extensions.MiiExStudieAkronym(new FhirString(studyDraft.Acronym))
            );
        }

        study.Identifier.Add(
            new Identifier(
                FhirConstants.UrlUiCreatedResearchStudyIdentifier,
                uiCreatedIdentifierValue
            )
        );
        var studyId = ComputeIdentifierHashId(
            FhirConstants.UrlUiCreatedResearchStudyIdentifier,
            uiCreatedIdentifierValue
        );
        study.Id = studyId;
        var studyEntry = new Bundle.EntryComponent
        {
            Resource = study,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.PUT,
                Url = $"ResearchStudy/{studyId}",
            },
        };

        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };
        bundle.Entry.Add(studyEntry);
        bundle.Entry.Add(groupEntry);
        bundle.Entry.AddRange(libraryEntries);

        return bundle;
    }

    /// <summary>
    /// Update-as-create id for a resource identified by (system, value): a lowercase-hex SHA-256
    /// digest of "system|value", used as both the resource id and the PUT url so re-submitting the
    /// same identifier always targets the same resource instead of creating a duplicate.
    /// </summary>
    private static string ComputeIdentifierHashId(string system, string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{system}|{value}")));
}
