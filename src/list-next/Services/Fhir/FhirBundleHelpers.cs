using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using list.Models;

namespace list.Services.Fhir;

internal static class FhirBundleHelpers
{
    /// <summary>
    /// Issues a GET against the given relative FHIR url and follows "next" links, returning
    /// every resource across all pages. Mirrors the "pageLimit: 0" behavior list-old relied on.
    /// </summary>
    public static async Task<List<Resource>> GetAllPagesAsync(
        FhirClient client,
        string relativeUrl,
        CancellationToken ct = default
    )
    {
        var resources = new List<Resource>();

        var result = await client.GetAsync(relativeUrl, ct);
        var bundle = result as Bundle;

        while (bundle is not null)
        {
            resources.AddRange(
                bundle.Entry.Where(e => e.Resource is not null).Select(e => e.Resource!)
            );

            if (bundle.NextLink is null)
            {
                break;
            }

            bundle = await client.ContinueAsync(bundle, PageDirection.Next, ct);
        }

        return resources;
    }

    public static string? GetStringExtension(this IExtendable element, string url) =>
        (element.GetExtension(url)?.Value as FhirString)?.Value
        ?? (element.GetExtension(url)?.Value as PrimitiveType)?.ToString();

    public static ResourceReference? GetReferenceExtension(this IExtendable element, string url) =>
        element.GetExtension(url)?.Value as ResourceReference;

    /// <summary>All repeating extensions at the given url whose value is an Annotation (author/time/text).</summary>
    public static IReadOnlyList<Annotation> GetAnnotationExtensions(
        this IExtendable element,
        string url
    ) =>
        (element.Extension ?? [])
            .Where(e => e.Url == url)
            .Select(e => e.Value as Annotation)
            .OfType<Annotation>()
            .ToList();

    /// <summary>Extracts the bare logical id (e.g. "abc123") from a relative reference (e.g. "ResearchStudy/abc123").</summary>
    public static string? GetReferencedId(this ResourceReference? reference) =>
        reference?.Reference?.Split('/').LastOrDefault();

    public static TrialIdentifier? GetTrialIdentifier(this ResearchStudy study) =>
        study.Identifier?.FirstOrDefault(i =>
            !string.IsNullOrEmpty(i.System) && !string.IsNullOrEmpty(i.Value)
        )
            is { System: { Length: > 0 } system, Value: { Length: > 0 } value }
            ? new TrialIdentifier(system, value)
            : null;

    public static string? GetStudyAcronym(this ResearchStudy study) =>
        study.GetStringExtension(FhirConstants.UrlStudyAcronym) is { Length: > 0 } acronym
            ? acronym
            : study.Title ?? study.Id;

    /// <summary>Decodes a criterion Library's SQL source from its content attachment.</summary>
    public static string? GetSqlText(this Library library) =>
        library.Content?.FirstOrDefault()?.Data is { Length: > 0 } data
            ? Encoding.UTF8.GetString(data)
            : null;

    public static string? GetMedicalRecordNumber(this Patient patient) =>
        patient.Identifier.FirstOrDefault(IsMedicalRecordNumber)?.Value;

    private static bool IsMedicalRecordNumber(Identifier identifier) =>
        identifier.Type?.Coding.Any(c =>
            c.System == FhirConstants.SystemIdentifierType
            && c.Code == FhirConstants.IdentifierTypeMedicalRecordNumber
        )
            is true;
}
