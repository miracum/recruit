namespace list.Services.Notify;

/// <summary>
/// A way of getting a rendered notification to a recipient. Email is the only implementation this
/// pass (EmailNotificationChannel) - this interface is what makes WhatsApp/DECT-SMS additive later
/// without touching detection (ScreeningListPollService) or recipient-resolution logic.
/// </summary>
public interface INotificationChannel
{
    Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken ct = default);
}
