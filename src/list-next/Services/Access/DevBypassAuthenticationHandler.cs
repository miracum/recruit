using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace list.Services.Access;

/// <summary>
/// Dev-only stand-in for OIDC (see Options.AuthOptions.Disabled), mirroring list-old's
/// KEYCLOAK_DISABLED behavior: treat every request as an authenticated admin so the UI is fully
/// usable without a running identity provider. Registered as a real authentication scheme (rather
/// than only a Blazor AuthenticationStateProvider) because Blazor Web App endpoints enforce
/// [Authorize] at the HTTP routing level too, which requires a working IAuthenticationService.
/// Must never be registered outside Development.
/// </summary>
public sealed class DevBypassAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevBypass";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "dev-admin"),
                new Claim("sub", "dev-admin"),
                new Claim("preferred_username", "dev-admin"),
                new Claim("email", "dev-admin@localhost"),
                new Claim("role", TrialAccessService.AdminRole),
            ],
            authenticationType: SchemeName,
            nameType: "preferred_username",
            roleType: "role"
        );

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
