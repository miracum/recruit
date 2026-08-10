using System.Security.Claims;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using list.Services.Auth;

namespace list.Services.Fhir;

/// <summary>
/// Ensures a FHIR Practitioner resource exists for the signed-in app user, keyed by their OIDC
/// "sub" (see FhirConstants.SystemPractitionerOidcSubject) - stable even if the user's email or
/// display name later changes.
/// </summary>
public sealed class PractitionerService(FhirClientFactory clientFactory, ILogger<PractitionerService> logger)
{
    /// <summary>
    /// Conditionally creates or updates (PUT Practitioner?identifier=...) the current user's
    /// Practitioner, keeping name/email in sync with their current claims.
    /// </summary>
    /// <returns>A reference to the Practitioner, or null if the user has no "sub" claim to key on.</returns>
    public async Task<ResourceReference?> EnsureCurrentPractitionerAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var subjectId = GetSubjectId(user);
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return null;
        }

        var practitioner = new Practitioner
        {
            Identifier = [new Identifier(FhirConstants.SystemPractitionerOidcSubject, subjectId)],
        };

        var email = GetEmail(user);
        if (!string.IsNullOrWhiteSpace(email))
        {
            practitioner.Telecom = [new ContactPoint(ContactPoint.ContactPointSystem.Email, ContactPoint.ContactPointUse.Work, email)];
        }

        var displayName = user.GetDisplayName();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            practitioner.Name = [new HumanName { Text = displayName }];
        }

        var condition = new SearchParams().Add("identifier", $"{FhirConstants.SystemPractitionerOidcSubject}|{subjectId}");

        try
        {
            var client = clientFactory.CreateClient();
            var result = await client.ConditionalUpdateAsync(practitioner, condition, ct: ct);
            return result?.Id is { Length: > 0 } id ? new ResourceReference($"Practitioner/{id}") : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to conditionally create/update Practitioner for subject {SubjectId}", subjectId);
            return null;
        }
    }

    private static string? GetSubjectId(ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private static string? GetEmail(ClaimsPrincipal user) =>
        user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;
}
