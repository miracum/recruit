using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Recruit.List.Web.Auth;

/// <summary>
/// Authenticates every request as a fixed local user without contacting Keycloak.
/// Used only when KEYCLOAK_DISABLED is set, e.g. for local development or the isolated
/// end-to-end smoke tests where standing up a Keycloak instance isn't warranted.
/// </summary>
public sealed class NoAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "NoAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "dev"), new Claim("preferred_username", "dev")],
            SchemeName
        );

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
