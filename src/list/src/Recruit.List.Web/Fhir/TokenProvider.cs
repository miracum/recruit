namespace Recruit.List.Web.Fhir;

/// <summary>
/// Holds the current user's access token for the lifetime of a single Blazor Server
/// circuit. <see cref="HttpContext"/> is only available while the component tree is
/// prerendered as part of the initial HTTP request; the token captured then is cached
/// here (registered as scoped, i.e. one instance per circuit) so it remains available
/// for the FHIR calls made afterwards, once the circuit is running over SignalR and
/// there is no HttpContext anymore.
/// </summary>
public sealed class TokenProvider
{
    public string? AccessToken { get; set; }
}
