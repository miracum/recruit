using Hl7.Fhir.Rest;
using list.Options;
using Microsoft.Extensions.Options;

namespace list.Services.Fhir;

/// <summary>
/// Builds a Firely SDK FhirClient using the "fhir" named HttpClient. One instance per
/// request/circuit scope is fine - the underlying HttpClient is pooled by IHttpClientFactory.
/// </summary>
public sealed class FhirClientFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<FhirOptions> fhirOptions
)
{
    public const string HttpClientName = "fhir";

    public FhirClient CreateClient()
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var settings = new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json,
            VerifyFhirVersion = false,
        };

        return new FhirClient(fhirOptions.Value.BaseUrl, httpClient, settings);
    }
}
