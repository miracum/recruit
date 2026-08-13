using Hangfire.Dashboard;
using list.Services.Access;

namespace list.Services.Notify;

/// <summary>
/// Gates /hangfire to admins, same role check as the "Admin" authorization policy used elsewhere.
/// Hangfire's dashboard middleware sits outside ASP.NET Core's authorization pipeline, so it needs
/// its own filter rather than an [Authorize] attribute or MapGroup policy.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter(bool authDisabled) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        if (authDisabled)
        {
            return true;
        }

        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true && TrialAccessService.IsAdmin(httpContext.User);
    }
}
