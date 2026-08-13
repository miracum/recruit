namespace list.Models;

/// <summary>Delivery channel for a NotificationRecipient. Email is the only value for now - see
/// INotificationChannel for how additional channels (WhatsApp, DECT SMS) would be added later.</summary>
public enum NotificationChannel
{
    Email = 0,
}
