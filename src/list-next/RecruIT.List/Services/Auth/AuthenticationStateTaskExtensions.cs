using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace RecruIT.List.Services.Auth;

public static class AuthenticationStateTaskExtensions
{
    /// <summary>
    /// Resolves a cascaded AuthStateTask to its ClaimsPrincipal, falling back to an empty (anonymous)
    /// principal when no AuthenticationState cascading parameter was supplied - the same
    /// null-coalescing every page/component needs when reading the current user.
    /// </summary>
    public static async Task<ClaimsPrincipal> GetUserAsync(
        this Task<AuthenticationState>? authStateTask
    ) => authStateTask is not null ? (await authStateTask).User : new ClaimsPrincipal();
}
