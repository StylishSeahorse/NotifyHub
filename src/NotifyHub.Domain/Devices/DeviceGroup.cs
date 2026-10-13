using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Devices;

public sealed class DeviceGroup : ITenantOwned
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;

    private DeviceGroup()
    {
        Name = null!;
        NormalizedName = null!;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Upper-invariant form of <see cref="Name"/>, unique per tenant.</summary>
    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DeviceGroup Create(Guid tenantId, string name, string? description, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        var group = new DeviceGroup
        {
            Id = Ids.New(),
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            CreatedAt = utcNow,
        };
        group.Update(name, description, now);
        return group;
    }

    public void Update(string name, string? description, DateTimeOffset now)
    {
        Name = Guard.Text(name, "Group name", NameMaxLength);
        NormalizedName = Name.ToUpperInvariant();
        Description = Guard.OptionalText(description, "Description", DescriptionMaxLength);
        UpdatedAt = Guard.Utc(now);
    }

    public DeviceGroupMember AddMember(Device device, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (device.TenantId != TenantId)
        {
            throw new DomainException("tenant.mismatch", "A device can only join groups in its own tenant.");
        }

        if (device.Status == DeviceStatus.Revoked)
        {
            throw new DomainException("device.revoked", "A revoked device cannot be added to a group.");
        }

        return new DeviceGroupMember(TenantId, Id, device.Id, Guard.Utc(now));
    }
}

public sealed class DeviceGroupMember : ITenantOwned
{
    internal DeviceGroupMember(Guid tenantId, Guid deviceGroupId, Guid deviceId, DateTimeOffset addedAt)
    {
        TenantId = tenantId;
        DeviceGroupId = deviceGroupId;
        DeviceId = deviceId;
        AddedAt = addedAt;
    }

    public Guid TenantId { get; private set; }

    public Guid DeviceGroupId { get; private set; }

    public Guid DeviceId { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }
}
