using System.Collections.Concurrent;
using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;

namespace list.Services.Auth;

/// <summary>
/// Blazor Server circuits have no HTTP response to write cookies to mid-stream, so
/// Duende.AccessTokenManagement needs a server-side store instead of its cookie-backed default.
/// This is an in-memory store (tokens are lost on app restart, forcing re-login) - acceptable for
/// a single-instance deployment; a multi-instance deployment would need a distributed store.
/// </summary>
public sealed class ServerSideTokenStore : IUserTokenStore
{
    private static readonly ConcurrentDictionary<string, TokenForParameters> Tokens = new();

    public Task<TokenResult<TokenForParameters>> GetTokenAsync(
        ClaimsPrincipal user, UserTokenRequestParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        var sub = GetSubjectId(user);
        return Task.FromResult(Tokens.TryGetValue(sub, out var value)
            ? TokenResult.Success(value)
            : (TokenResult<TokenForParameters>)TokenResult.Failure("not found"));
    }

    public Task StoreTokenAsync(
        ClaimsPrincipal user, UserToken token, UserTokenRequestParameters? parameters = null, CancellationToken ct = default)
    {
        var sub = GetSubjectId(user);
        Tokens[sub] = new TokenForParameters(
            token,
            token.RefreshToken is null ? null : new UserRefreshToken(token.RefreshToken.Value, token.DPoPJsonWebKey));

        return Task.CompletedTask;
    }

    public Task ClearTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null, CancellationToken ct = default)
    {
        Tokens.TryRemove(GetSubjectId(user), out _);
        return Task.CompletedTask;
    }

    private static string GetSubjectId(ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value
        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("The current user has no 'sub' or NameIdentifier claim.");
}
