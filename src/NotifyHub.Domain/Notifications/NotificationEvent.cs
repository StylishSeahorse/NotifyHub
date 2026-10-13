using System.Text.RegularExpressions;
using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Notifications;

/// <summary>
/// Immutable, client-reported event. (device, idempotency key) is unique so retried uploads never create
/// duplicate state transitions. <see cref="OccurredAt"/> is stored as reported for forensics; state
/// transitions use it clamped to <see cref="ReceivedAt"/>.
/// </summary>
public sealed partial class NotificationEvent : ITenantOwned
{
    public const int IdempotencyKeyMaxLength = 128;
    public const int MetadataMaxLength = 4096;

    private NotificationEvent()
    {
        IdempotencyKey = null!;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid NotificationId { get; private set; }

    public Guid DeviceId { get; private set; }

    public Guid? RecipientId { get; private set; }

    public NotificationEventType EventType { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public string IdempotencyKey { get; private set; }

    /// <summary>Optional JSON object with diagnostic detail. Never interpreted.</summary>
    public string? EventMetadata { get; private set; }

    public static NotificationEvent Record(
        Guid tenantId,
        Guid notificationId,
        Guid deviceId,
        Guid? recipientId,
        NotificationEventType eventType,
        string idempotencyKey,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt,
        string? eventMetadata = null)
    {
        if (!Enum.IsDefined(eventType))
        {
            throw new DomainException("event.invalid_type", "Unknown event type.");
        }

        var key = idempotencyKey?.Trim() ?? string.Empty;
        if (key.Length is 0 or > IdempotencyKeyMaxLength || !KeyPattern().IsMatch(key))
        {
            throw new DomainException("event.invalid_idempotency_key", $"Idempotency key must be 1–{IdempotencyKeyMaxLength} characters of letters, digits, '-', '_', '.' or ':'.");
        }

        if (eventMetadata is not null && eventMetadata.Length > MetadataMaxLength)
        {
            throw new DomainException("event.metadata_too_large", $"Event metadata must be at most {MetadataMaxLength} characters.");
        }

        return new NotificationEvent
        {
            Id = Ids.New(),
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            NotificationId = Guard.NotEmpty(notificationId, "Notification"),
            DeviceId = Guard.NotEmpty(deviceId, "Device"),
            RecipientId = recipientId,
            EventType = eventType,
            IdempotencyKey = key,
            OccurredAt = Guard.Utc(occurredAt),
            ReceivedAt = Guard.Utc(receivedAt),
            EventMetadata = eventMetadata,
        };
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
