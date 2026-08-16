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
}
