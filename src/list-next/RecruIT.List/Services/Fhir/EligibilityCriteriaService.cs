using System.Security.Cryptography;
using System.Text;
using De.Medizininformatikinitiative.Kerndatensatz.Studie;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Localization;
using RecruIT.List.Models;
using RecruIT.List.Resources;
using Task = System.Threading.Tasks.Task;

namespace RecruIT.List.Services.Fhir;

/// <summary>
/// Backs the admin-only Criteria Manager page: lets an admin browse existing ResearchStudy and
/// Library resources, assemble the FHIR resources (ResearchStudy, Group, one Library per criterion)
/// that a study's eligibility criteria would consist of, per
/// docs/trino/eligibility-criteria-design.md, and submit that same Bundle as a transaction against
/// the FHIR server.
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
    /// Every ResearchStudy resource returned by the last SearchResearchStudiesAsync call, keyed by
    /// id - lets BuildPreviewBundle embed the exact, unmodified resource for a study picked via the
    /// "use an existing study" flow without a second (and necessarily synchronous, since preview
    /// building happens from a Razor property getter) round trip to the FHIR server.
    /// </summary>
    private Dictionary<string, ResearchStudy> _researchStudiesById = [];

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
        List<Resource> libraries;
        List<Resource> groups;
        List<Resource> studies;
        try
        {
            // Independent searches, run concurrently - each gets its own FhirClient rather than
            // sharing one, since BaseFhirClient tracks last-request state (LastResult etc.) as
            // mutable instance fields that aren't safe to touch from concurrent calls.
            var librariesTask = FhirBundleHelpers.GetAllPagesAsync(
                clientFactory.CreateClient(),
                $"Library?type={Uri.EscapeDataString($"{SqlQueryLibraryTypeSystem}|{SqlQueryLibraryTypeCode}")}"
                    + "&_count=100&_sort=-_lastUpdated",
                ct
            );
            var groupsTask = FhirBundleHelpers.GetAllPagesAsync(
                clientFactory.CreateClient(),
                "Group?_count=100",
                ct
            );
            var studiesTask = FhirBundleHelpers.GetAllPagesAsync(
                clientFactory.CreateClient(),
                "ResearchStudy?_count=100",
                ct
            );

            await Task.WhenAll(librariesTask, groupsTask, studiesTask);
            libraries = await librariesTask;
            groups = await groupsTask;
            studies = await studiesTask;
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
    /// Lists every ResearchStudy resource on the FHIR server, for the "use an existing study"
    /// picker - caches the full resources by id so BuildPreviewBundle can later embed whichever one
    /// gets picked without another round trip.
    /// </summary>
    public async Task<IReadOnlyList<ResearchStudySummaryDto>> SearchResearchStudiesAsync(
        CancellationToken ct = default
    )
    {
        List<Resource> resources;
        try
        {
            resources = await FhirBundleHelpers.GetAllPagesAsync(
                clientFactory.CreateClient(),
                "ResearchStudy?_count=100&_sort=-_lastUpdated",
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search ResearchStudy resources");
            throw new FhirAccessException(localizer["App.Errors.ResearchStudiesLoadFailed"], ex);
        }

        var studies = resources.OfType<ResearchStudy>().Where(s => s.Id is not null).ToList();
        _researchStudiesById = studies.ToDictionary(s => s.Id!, s => s);

        return studies
            .Select(s => new ResearchStudySummaryDto(
                s.Id!,
                s.Title,
                s.GetStudyAcronym() is { Length: > 0 } acronym ? acronym : null,
                s.Description,
                s.Status?.ToString()
            ))
            .OrderBy(s => s.Title ?? s.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

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

    /// <summary>
    /// Assembles the same Bundle as BuildPreviewJson and actually submits it as a transaction
    /// against the FHIR server, upserting every resource in it at once.
    /// </summary>
    public async Task SubmitAsync(
        ResearchStudyDraft studyDraft,
        IReadOnlyList<CriterionDraft> criteria,
        CancellationToken ct = default
    )
    {
        var bundle = BuildPreviewBundle(studyDraft, criteria);
        var client = clientFactory.CreateClient();

        try
        {
            await client.TransactionAsync(bundle, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit eligibility criteria transaction bundle");
            throw new FhirAccessException(localizer["App.Errors.CriteriaSubmitFailed"], ex);
        }
    }

    private Bundle BuildPreviewBundle(
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
                var libraryId = ComputeIdentifierHashId(
                    FhirConstants.UrlUiCreatedEligibilityLibraryIdentifier,
                    name
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
                    new Identifier(FhirConstants.UrlUiCreatedEligibilityLibraryIdentifier, name)
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

        // A UI-assigned identifier that anchors the Group's identity, shared with the ResearchStudy
        // below when it's newly-authored (each under their own resource-specific system, so they
        // still hash to different ids) - same story as the ad hoc Library criteria above. For a
        // study picked via the "use an existing study" flow, its own FHIR id anchors the Group's
        // identity instead, so re-authoring criteria for that study always targets the same Group.
        var groupIdentifierValue = studyDraft.ExistingStudyId ?? Slug.Create(studyDraft.Title);

        group.Identifier.Add(
            new Identifier(FhirConstants.UrlEligibilityGroupIdentifier, groupIdentifierValue)
        );
        var groupId = ComputeIdentifierHashId(
            FhirConstants.UrlEligibilityGroupIdentifier,
            groupIdentifierValue
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

        var studyEntry = studyDraft.ExistingStudyId is { } existingStudyId
            ? BuildExistingStudyEntry(existingStudyId, groupReference)
            : BuildNewStudyEntry(studyDraft, groupReference);

        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };
        bundle.Entry.Add(studyEntry);
        bundle.Entry.Add(groupEntry);
        bundle.Entry.AddRange(libraryEntries);

        return bundle;
    }

    /// <summary>
    /// Embeds the picked ResearchStudy exactly as last fetched by SearchResearchStudiesAsync, except
    /// that Enrollment is replaced with a single reference to the criteria Group - a study only ever
    /// screens against the one eligibility Group this page is authoring, so any previously-enrolled
    /// Group(s) (this page's own past output, or otherwise) are dropped rather than kept alongside it.
    /// </summary>
    private Bundle.EntryComponent BuildExistingStudyEntry(
        string existingStudyId,
        ResourceReference groupReference
    )
    {
        if (!_researchStudiesById.TryGetValue(existingStudyId, out var existingStudy))
        {
            throw new InvalidOperationException(
                $"ResearchStudy/{existingStudyId} is not among the last-searched studies."
            );
        }

        var study = (ResearchStudy)existingStudy.DeepCopy();
        study.Enrollment = [groupReference];

        return new Bundle.EntryComponent
        {
            Resource = study,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.PUT,
                Url = $"ResearchStudy/{study.Id}",
            },
        };
    }

    private static Bundle.EntryComponent BuildNewStudyEntry(
        ResearchStudyDraft studyDraft,
        ResourceReference groupReference
    )
    {
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

        // Always computable (Title is never null), so a newly-authored study always gets a
        // deterministic update-as-create id, never a plain POST with a server-assigned one.
        var uiCreatedIdentifierValue = Slug.Create(studyDraft.Title);
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

        return new Bundle.EntryComponent
        {
            Resource = study,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.PUT,
                Url = $"ResearchStudy/{studyId}",
            },
        };
    }

    /// <summary>
    /// Update-as-create id for a resource identified by (system, value): a lowercase-hex SHA-256
    /// digest of "system|value", used as both the resource id and the PUT url so re-submitting the
    /// same identifier always targets the same resource instead of creating a duplicate.
    /// </summary>
    private static string ComputeIdentifierHashId(string system, string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{system}|{value}")));
}
