using System.Security.Claims;

namespace RecruIT.List.Services.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The user's full display name (OIDC "name" claim). Falls back to whatever NameClaimType
    /// resolves to - "preferred_username" per Program.cs's OIDC config - only if "name" is unset,
    /// so UI surfaces show a real name ("Jane Doe") rather than a login handle ("jdoe") wherever possible.
    /// </summary>
    public static string? GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirst("name")?.Value
        ?? user.FindFirst(ClaimTypes.Name)?.Value
        ?? user.Identity?.Name;

    /// <summary>OIDC "sub" claim, falling back to the standard NameIdentifier claim type.</summary>
    public static string? GetSubjectId(this ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>OIDC "email" claim, falling back to the standard Email claim type.</summary>
    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;

    /// <summary>
    /// The two identities a user's TrialAccessGrant/NotificationSubscription rows are looked up by
    /// - sub is primary, email is the fallback for grants created before the user's first login
    /// (see OidcEvents, which backfills SubjectId once they do log in).
    /// </summary>
    public static (string? SubjectId, string? Email) GetUserKeys(this ClaimsPrincipal user) =>
        (user.GetSubjectId(), user.GetEmail());
}
