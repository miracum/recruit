namespace list.Services.Fhir;

/// <summary>
/// Thrown by the FHIR service layer for any failure (network, server error, timeout) that should
/// be surfaced to the user as a friendly message rather than a raw exception/stack trace.
/// </summary>
public sealed class FhirAccessException : Exception
{
    public FhirAccessException(string message)
        : base(message) { }

    public FhirAccessException(string message, Exception innerException)
        : base(message, innerException) { }
}
