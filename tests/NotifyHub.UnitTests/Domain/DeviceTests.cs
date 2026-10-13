using NotifyHub.Domain.Common;
using NotifyHub.Domain.Devices;
using static NotifyHub.UnitTests.Domain.TestData;

namespace NotifyHub.UnitTests.Domain;

public sealed class DeviceTests
{
    [Fact]
    public void Registered_device_cannot_authenticate_until_enrolled()
    {
        var device = Device.Register(TenantA, "LAB-PC-01", T0);

        Assert.Equal(DeviceStatus.Pending, device.Status);
        Assert.False(device.CanAuthenticate);

        device.CompleteEnrolment("key-1", "Windows 11 Enterprise 24H2", "0.1.0", T0.AddMinutes(1));

        Assert.True(device.CanAuthenticate);
        Assert.Equal("key-1", device.DeviceKeyId);
        Assert.Equal(T0.AddMinutes(1), device.LastSeenAt);
    }

    [Fact]
    public void Enrolment_cannot_be_repeated()
    {
        var device = Enrolled();

        Assert.Throws<DomainException>(() => device.CompleteEnrolment("key-2", null, null, T0));
    }

    [Fact]
    public void Revoked_device_is_rejected_permanently()
    {
        var device = Enrolled();

        device.Revoke(T0.AddHours(1));

        Assert.Equal(DeviceStatus.Revoked, device.Status);
        Assert.False(device.CanAuthenticate);
        Assert.Equal(T0.AddHours(1), device.RevokedAt);
        Assert.Throws<DomainException>(() => device.RecordCheckIn(null, null, T0.AddHours(2)));
        Assert.Throws<DomainException>(() => device.Reinstate());
        Assert.Throws<DomainException>(() => device.RotateCredential("key-9"));
    }

    [Fact]
    public void Quarantine_blocks_authentication_until_reinstated()
    {
        var device = Enrolled();

        device.Quarantine();
        Assert.False(device.CanAuthenticate);

        device.Reinstate();
        Assert.True(device.CanAuthenticate);
    }

    [Fact]
    public void Credential_rotation_requires_new_key_id()
    {
        var device = Enrolled();

        Assert.Equal("device.key_reuse", Assert.Throws<DomainException>(() => device.RotateCredential("key-1")).Code);
        device.RotateCredential("key-2");
        Assert.Equal("key-2", device.DeviceKeyId);
    }

    [Fact]
    public void Group_membership_is_restricted_to_the_same_tenant()
    {
        var group = DeviceGroup.Create(TenantA, "Reception", null, T0);
        var foreignDevice = Device.Register(TenantB, "OTHER-PC", T0);

        Assert.Equal("tenant.mismatch", Assert.Throws<DomainException>(() => group.AddMember(foreignDevice, T0)).Code);
    }

    [Fact]
    public void Revoked_device_cannot_join_group()
    {
        var group = DeviceGroup.Create(TenantA, "Reception", null, T0);
        var device = Enrolled();
        device.Revoke(T0);

        Assert.Throws<DomainException>(() => group.AddMember(device, T0));
    }

    [Fact]
    public void Group_name_is_normalised_for_uniqueness()
    {
        var group = DeviceGroup.Create(TenantA, "  Front Desk ", null, T0);

        Assert.Equal("Front Desk", group.Name);
        Assert.Equal("FRONT DESK", group.NormalizedName);
    }

    private static Device Enrolled()
    {
        var device = Device.Register(TenantA, "LAB-PC-01", T0);
        device.CompleteEnrolment("key-1", null, null, T0);
        return device;
    }
}
