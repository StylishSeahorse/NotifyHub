using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Devices;

public enum DeviceStatus
{
    /// <summary>Registered by an administrator; no credential has been issued yet.</summary>
    Pending,

    Active,

    /// <summary>Temporarily blocked; credentials are rejected but the record can be reinstated.</summary>
    Quarantined,

    /// <summary>Permanently revoked. Terminal.</summary>
    Revoked,
}

/// <summary>
/// An enrolled Windows computer. Raw credentials are never stored here; <see cref="DeviceKeyId"/> is the
/// public identifier of the device's current credential (credential material lives in a separate store, Phase 3).
/// </summary>
public sealed class Device : ITenantOwned
{
    public const int NameMaxLength = 255;
    public const int KeyIdMaxLength = 100;
    public const int OperatingSystemMaxLength = 200;
    public const int AgentVersionMaxLength = 50;

    private Device()
    {
        DeviceName = null!;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string DeviceName { get; private set; }

    public string? DeviceKeyId { get; private set; }

    public string? OperatingSystem { get; private set; }

    public string? AgentVersion { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public DeviceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? EnrolledAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static Device Register(Guid tenantId, string deviceName, DateTimeOffset now) => new()
    {
        Id = Ids.New(),
        TenantId = Guard.NotEmpty(tenantId, "Tenant"),
        DeviceName = Guard.Text(deviceName, "Device name", NameMaxLength),
        Status = DeviceStatus.Pending,
        CreatedAt = Guard.Utc(now),
    };

    /// <summary>Whether the device may authenticate. Only active devices are accepted.</summary>
    public bool CanAuthenticate => Status == DeviceStatus.Active;

    public void CompleteEnrolment(string deviceKeyId, string? operatingSystem, string? agentVersion, DateTimeOffset now)
    {
        if (Status != DeviceStatus.Pending)
        {
            throw new DomainException("device.invalid_state", $"A device in state {Status} cannot be enrolled.");
        }

        DeviceKeyId = Guard.Text(deviceKeyId, "Device key ID", KeyIdMaxLength);
        Status = DeviceStatus.Active;
        EnrolledAt = Guard.Utc(now);
        RecordCheckIn(operatingSystem, agentVersion, now);
    }

    public void RotateCredential(string newDeviceKeyId)
    {
        if (Status != DeviceStatus.Active)
        {
            throw new DomainException("device.invalid_state", $"A device in state {Status} cannot rotate credentials.");
        }

        var keyId = Guard.Text(newDeviceKeyId, "Device key ID", KeyIdMaxLength);
        if (keyId == DeviceKeyId)
        {
            throw new DomainException("device.key_reuse", "A rotated credential must have a new key identifier.");
        }

        DeviceKeyId = keyId;
    }

    public void RecordCheckIn(string? operatingSystem, string? agentVersion, DateTimeOffset now)
    {
        if (Status != DeviceStatus.Active)
        {
            throw new DomainException("device.not_active", "Only active devices can check in.");
        }

        OperatingSystem = Guard.OptionalText(operatingSystem, "Operating system", OperatingSystemMaxLength) ?? OperatingSystem;
        AgentVersion = Guard.OptionalText(agentVersion, "Agent version", AgentVersionMaxLength) ?? AgentVersion;
        LastSeenAt = Guard.Utc(now);
    }

    public void Rename(string deviceName) => DeviceName = Guard.Text(deviceName, "Device name", NameMaxLength);

    public void Quarantine()
    {
        if (Status is not DeviceStatus.Active)
        {
            throw new DomainException("device.invalid_state", $"A device in state {Status} cannot be quarantined.");
        }

        Status = DeviceStatus.Quarantined;
    }

    public void Reinstate()
    {
        if (Status is not DeviceStatus.Quarantined)
        {
            throw new DomainException("device.invalid_state", $"A device in state {Status} cannot be reinstated.");
        }

        Status = DeviceStatus.Active;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (Status == DeviceStatus.Revoked)
        {
            return;
        }

        Status = DeviceStatus.Revoked;
        RevokedAt = Guard.Utc(now);
    }
}
