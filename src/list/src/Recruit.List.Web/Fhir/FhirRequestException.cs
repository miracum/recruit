namespace Recruit.List.Web.Fhir;

public sealed class FhirRequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
