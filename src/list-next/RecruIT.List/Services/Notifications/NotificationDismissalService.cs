using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using RecruIT.List.Models;

namespace RecruIT.List.Services.Notifications;

/// <summary>
/// Tracks which notifications the current browser has dismissed, using Blazor Server's built-in
/// signed/encrypted localStorage wrapper. No database is used for this yet (see the "explicitly
/// out of scope" note in the implementation plan) - this is per-browser, not cross-device.
/// Must only be called after the component has rendered at least once (JS interop is unavailable
/// during prerendering).
/// </summary>
public sealed class NotificationDismissalService(ProtectedLocalStorage storage)
{
    private const string StorageKey = "recruit-list.dismissed-notifications";

    public async Task<HashSet<string>> GetDismissedIdsAsync()
    {
        try
        {
            var result = await storage.GetAsync<string[]>(StorageKey);
            return result.Success && result.Value is not null ? [.. result.Value] : [];
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet (server-side prerendering) - treat as nothing dismissed.
            return [];
        }
    }

    public async Task DismissAsync(string notificationId)
    {
        var dismissed = await GetDismissedIdsAsync();
        dismissed.Add(notificationId);
        await storage.SetAsync(StorageKey, dismissed.ToArray());
    }

    public async Task<IReadOnlyList<NotificationEventDto>> FilterActiveAsync(
        IEnumerable<NotificationEventDto> events
    )
    {
        var dismissed = await GetDismissedIdsAsync();
        return events.Where(e => !dismissed.Contains(e.Id)).ToList();
    }
}
