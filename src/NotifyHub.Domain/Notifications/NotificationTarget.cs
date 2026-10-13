using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Notifications;

/// <summary>
/// Requested audience entry. Exactly one reference column is set according to <see cref="TargetType"/>
/// (none for <see cref="NotificationTargetType.AllDevices"/>); each has its own tenant-consistent foreign key.
/// </summary>
public sealed class NotificationTarget : ITenantOwned
{
    private NotificationTarget()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid NotificationId { get; private set; }

    public NotificationTargetType TargetType { get; private set; }

    public Guid? DeviceGroupId { get; private set; }

    public Guid? DeviceId { get; private set; }

    public Guid? RecipientId { get; private set; }

    /// <summary>The referenced entity, or null for <see cref="NotificationTargetType.AllDevices"/>.</summary>
    public Guid? TargetId => DeviceGroupId ?? DeviceId ?? RecipientId;

    internal static NotificationTarget Create(Guid tenantId, Guid notificationId, NotificationTargetSpec spec)
    {
        var target = new NotificationTarget
        {
            Id = Ids.New(),
            TenantId = tenantId,
            NotificationId = notificationId,
            TargetType = spec.TargetType,
        };

        switch (spec.TargetType)
        {
            case NotificationTargetType.DeviceGroup:
                target.DeviceGroupId = spec.TargetId;
                break;
            case NotificationTargetType.Device:
                target.DeviceId = spec.TargetId;
                break;
            case NotificationTargetType.Recipient:
                target.RecipientId = spec.TargetId;
                break;
            case NotificationTargetType.AllDevices:
                break;
            default:
                throw new DomainException("target.invalid_type", "Unknown target type.");
        }

        return target;
    }

    public NotificationTargetSpec ToSpec() => new(TargetType, TargetId);
}

/// <summary>An unvalidated target request. Tenant ownership of <see cref="TargetId"/> is verified by the application layer and the database.</summary>
public sealed record NotificationTargetSpec(NotificationTargetType TargetType, Guid? TargetId)
{
    public static NotificationTargetSpec AllDevices { get; } = new(NotificationTargetType.AllDevices, null);

    public static NotificationTargetSpec Group(Guid id) => new(NotificationTargetType.DeviceGroup, id);

    public static NotificationTargetSpec ForDevice(Guid id) => new(NotificationTargetType.Device, id);

    public static NotificationTargetSpec ForRecipient(Guid id) => new(NotificationTargetType.Recipient, id);
}
