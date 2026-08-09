using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace list.Services.Auth;

/// <summary>Stashes the tokens returned at login into the (Blazor Server-safe) IUserTokenStore.</summary>
public sealed class OidcEvents(IUserTokenStore store, IConfiguration configuration) : OpenIdConnectEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var response = context.TokenEndpointResponse!;
        var expiresIn = double.Parse(response.ExpiresIn, System.Globalization.CultureInfo.InvariantCulture);
        var clientId = response.ClientId ?? configuration["Oidc:ClientId"] ?? string.Empty;

        await store.StoreTokenAsync(context.Principal!, new UserToken
        {
            AccessToken = AccessToken.Parse(response.AccessToken),
            AccessTokenType = AccessTokenType.Parse(response.TokenType),
            RefreshToken = string.IsNullOrEmpty(response.RefreshToken) ? null : RefreshToken.Parse(response.RefreshToken),
            Scope = string.IsNullOrEmpty(response.Scope) ? null : Scope.Parse(response.Scope),
            ClientId = ClientId.Parse(clientId),
            IdentityToken = string.IsNullOrEmpty(response.IdToken) ? null : IdentityToken.Parse(response.IdToken),
            Expiration = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
        });

        await base.TokenValidated(context);
    }
}
