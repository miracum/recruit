namespace RecruIT.List.Models;

/// <summary>Delivery channel for a NotificationDelivery. InApp needs no INotificationChannel (it's
/// just a row the bell/feed reads); Email goes through INotificationChannel for how additional
/// channels (WhatsApp, DECT SMS) would be added later.</summary>
public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
}
