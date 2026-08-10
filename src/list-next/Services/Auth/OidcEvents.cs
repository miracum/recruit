using System.Security.Claims;
using list.Data;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;

namespace list.Services.Auth;

/// <summary>
/// Runs once per real sign-in, before the auth cookie is issued - the one place claims can be
/// augmented with state that must survive for the whole session (unlike an IClaimsTransformation,
/// which would re-run on every request for no benefit, since Blazor Server re-validates the
/// existing cookie principal rather than the token on each request).
/// </summary>
public sealed class OidcEvents(IConfiguration configuration, IDbContextFactory<AppDbContext> dbContextFactory)
    : OpenIdConnectEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal!;

        ApplyGroupRoleMappings(principal);
        await ProvisionGrantsAsync(principal, context.HttpContext.RequestAborted);

        await base.TokenValidated(context);
    }

    /// <summary>
    /// Lets an IdP group double as an app role without needing IdP-side realm-role/client-scope
    /// setup: configure Oidc:GroupClaimType (default "groups") and Oidc:GroupRoleMappings (a
    /// { "&lt;group name or path&gt;": "&lt;role&gt;" } map, e.g. { "/recruit-admins": "admin" })
    /// and any matching group membership becomes a role claim of the app's configured RoleClaimType.
    /// </summary>
    private void ApplyGroupRoleMappings(ClaimsPrincipal principal)
    {
        var mappings = configuration.GetSection("Oidc:GroupRoleMappings").Get<Dictionary<string, string>>();
        if (mappings is not { Count: > 0 } || principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var groupClaimType = configuration["Oidc:GroupClaimType"] is { Length: > 0 } gct ? gct : "groups";
        var roleClaimType = configuration["Oidc:RoleClaimType"] is { Length: > 0 } rc ? rc : "role";
        var existingRoles = identity.FindAll(roleClaimType).Select(c => c.Value).ToHashSet();

        foreach (var groupClaim in identity.FindAll(groupClaimType))
        {
            if (mappings.TryGetValue(groupClaim.Value, out var role) && existingRoles.Add(role))
            {
                identity.AddClaim(new Claim(roleClaimType, role));
            }
        }
    }

    /// <summary>
    /// Activates any TrialAccessGrant an admin created by email before this person had ever logged
    /// in, by filling in the OIDC "sub" (and a display name) now that we have them.
    /// </summary>
    private async Task ProvisionGrantsAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var email = principal.FindFirst("email")?.Value ?? principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return;
        }

        var subjectId = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var displayName = principal.GetDisplayName();

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var pendingGrants = await db.TrialAccessGrants
            .Where(g => g.Email == email && g.SubjectId == null)
            .ToListAsync(ct);

        if (pendingGrants.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var grant in pendingGrants)
        {
            grant.SubjectId = subjectId;
            grant.DisplayName = displayName;
            grant.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
