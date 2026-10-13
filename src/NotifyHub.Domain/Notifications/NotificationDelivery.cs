using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Notifications;

/// <summary>
/// Server-side record of sending a notification to a device (optionally for a specific recipient).
/// Delivery to a device is not evidence that anyone saw the notice; see <see cref="NotificationEvent"/>.
/// </summary>
public sealed class NotificationDelivery : ITenantOwned
{
    private NotificationDelivery()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid NotificationId { get; private set; }

    public Guid DeviceId { get; private set; }

    public Guid? RecipientId { get; private set; }

    public DeliveryStatus DeliveryStatus { get; private set; }

    public DateTimeOffset FirstSentAt { get; private set; }

    public DateTimeOffset LastSentAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public int AttemptCount { get; private set; }

    public static NotificationDelivery Start(Notification notification, Guid deviceId, Guid? recipientId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var utcNow = Guard.Utc(now);
        return new NotificationDelivery
        {
            Id = Ids.New(),
            TenantId = notification.TenantId,
            NotificationId = notification.Id,
            DeviceId = Guard.NotEmpty(deviceId, "Device"),
            RecipientId = recipientId,
            DeliveryStatus = DeliveryStatus.Sent,
            FirstSentAt = utcNow,
            LastSentAt = utcNow,
            AttemptCount = 1,
        };
    }

    public void RecordAttempt(DateTimeOffset now)
    {
        if (DeliveryStatus == DeliveryStatus.Delivered)
        {
            return;
        }

        AttemptCount++;
        LastSentAt = Guard.Utc(now);
        DeliveryStatus = DeliveryStatus.Sent;
    }

    public void MarkDelivered(DateTimeOffset now)
    {
        if (DeliveryStatus == DeliveryStatus.Delivered)
        {
            return;
        }

        DeliveryStatus = DeliveryStatus.Delivered;
        DeliveredAt = Guard.Utc(now);
    }

    public void MarkFailed()
    {
        if (DeliveryStatus != DeliveryStatus.Delivered)
        {
            DeliveryStatus = DeliveryStatus.Failed;
        }
    }
}
