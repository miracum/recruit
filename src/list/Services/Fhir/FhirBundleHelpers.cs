using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace list.Services.Fhir;

internal static class FhirBundleHelpers
{
    /// <summary>
    /// Issues a GET against the given relative FHIR url and follows "next" links, returning
    /// every resource across all pages. Mirrors the "pageLimit: 0" behavior list-old relied on.
    /// </summary>
    public static async Task<List<Resource>> GetAllPagesAsync(FhirClient client, string relativeUrl, CancellationToken ct = default)
    {
        var resources = new List<Resource>();

        var result = await client.GetAsync(relativeUrl, ct).ConfigureAwait(false);
        var bundle = result as Bundle;

        while (bundle is not null)
        {
            resources.AddRange(bundle.Entry.Where(e => e.Resource is not null).Select(e => e.Resource!));

            if (bundle.NextLink is null)
            {
                break;
            }

            bundle = await client.ContinueAsync(bundle, PageDirection.Next, ct).ConfigureAwait(false);
        }

        return resources;
    }

    public static string? GetStringExtension(this IExtendable element, string url) =>
        (element.GetExtension(url)?.Value as FhirString)?.Value ?? (element.GetExtension(url)?.Value as PrimitiveType)?.ToString();

    public static ResourceReference? GetReferenceExtension(this IExtendable element, string url) =>
        element.GetExtension(url)?.Value as ResourceReference;
}
