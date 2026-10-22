namespace RecruIT.List.Services.Notify;

/// <summary>
/// A way of getting a rendered notification to a recipient. Email is the only implementation this
/// pass (EmailNotificationChannel) - this interface is what makes WhatsApp/DECT-SMS additive later
/// without touching detection (ScreeningListPollService) or recipient-resolution logic.
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// Opens whatever connection/session the channel needs for a run of sends - callers should
    /// open one batch per polling tick and send every due message through it, rather than one
    /// batch per message. For email specifically, this keeps a single authenticated SMTP session
    /// alive across a tick's worth of recipients instead of reconnecting and re-authenticating for
    /// every one, which is what real SMTP relays' per-source connection-rate limits actually
    /// police - a burst of one-off connections is what trips them, not the number of messages sent
    /// over an already-open session.
    /// </summary>
    /// <param name="protocolLog">
    /// Optional sink for the channel's wire-level conversation (for email, the SMTP transcript),
    /// left open afterwards - used by the admin test page to show exactly what the server said.
    /// </param>
    Task<INotificationBatch> BeginBatchAsync(
        Stream? protocolLog = null,
        CancellationToken ct = default
    );
}

public interface INotificationBatch : IAsyncDisposable
{
    Task SendAsync(
        string recipient,
        string subject,
        string htmlBody,
        CancellationToken ct = default
    );
}
