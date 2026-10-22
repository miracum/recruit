using System.Security.Claims;

namespace RecruIT.List.Services.Notify;

/// <summary>
/// The current circuit's unread in-app notification count, shared between MainLayout's sidebar
/// badge and the notifications page - so marking items read there updates the badge immediately,
/// instead of only on the next full page load. Same publish/subscribe shape as BreadcrumbState.
/// </summary>
public sealed class NotificationInboxState(NotificationSubscriptionService subscriptionService)
{
    public int UnreadCount { get; private set; }

    public event Action? Changed;

    public async Task RefreshAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        UnreadCount = await subscriptionService.GetUnreadInAppCountAsync(user, ct);
        Changed?.Invoke();
    }
}
