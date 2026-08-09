using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace list.Services.Auth;

/// <summary>
/// Rejects the auth cookie if its tokens have fallen out of the (in-memory) ServerSideTokenStore
/// - e.g. after an app restart - forcing a clean re-login instead of an app that's "logged in"
/// but can never call FHIR again. Also revokes the refresh token with the IdP on sign-out.
/// </summary>
public sealed class CookieEvents(IUserTokenStore store, ILogger<CookieEvents> logger) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var token = await store.GetTokenAsync(context.Principal!);
        if (!token.Succeeded)
        {
            context.RejectPrincipal();
        }

        await base.ValidatePrincipal(context);
    }

    public override async Task SigningOut(CookieSigningOutContext context)
    {
        try
        {
            await context.HttpContext.RevokeRefreshTokenAsync();
        }
        catch (Exception ex)
        {
            // Best-effort: some IdP/client configurations (e.g. a public client with no client
            // secret) can't authenticate to the token revocation endpoint. Sign-out should still
            // succeed regardless - the refresh token simply expires on its own otherwise.
            logger.LogWarning(ex, "Failed to revoke the refresh token on sign-out");
        }

        await base.SigningOut(context);
    }
}
