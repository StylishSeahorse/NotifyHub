using NotifyHub.Domain.Common;
using NotifyHub.Domain.Notifications;
using static NotifyHub.UnitTests.Domain.TestData;

namespace NotifyHub.UnitTests.Domain;

public sealed class TargetingTests
{
    [Fact]
    public void Targets_are_stored_with_typed_references()
    {
        var group = Guid.CreateVersion7();
        var device = Guid.CreateVersion7();
        var recipient = Guid.CreateVersion7();
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);

        n.ReplaceTargets([NotificationTargetSpec.Group(group), NotificationTargetSpec.ForDevice(device), NotificationTargetSpec.ForRecipient(recipient)], T0);

        Assert.Collection(
            n.Targets,
            t => Assert.Equal((NotificationTargetType.DeviceGroup, (Guid?)group, (Guid?)null, (Guid?)null), (t.TargetType, t.DeviceGroupId, t.DeviceId, t.RecipientId)),
            t => Assert.Equal((NotificationTargetType.Device, (Guid?)null, (Guid?)device, (Guid?)null), (t.TargetType, t.DeviceGroupId, t.DeviceId, t.RecipientId)),
            t => Assert.Equal((NotificationTargetType.Recipient, (Guid?)null, (Guid?)null, (Guid?)recipient), (t.TargetType, t.DeviceGroupId, t.DeviceId, t.RecipientId)));
        Assert.All(n.Targets, t => Assert.Equal(TenantA, t.TenantId));
    }

    [Fact]
    public void All_devices_cannot_be_combined_with_other_targets()
    {
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);

        var ex = Assert.Throws<DomainException>(() =>
            n.ReplaceTargets([NotificationTargetSpec.AllDevices, NotificationTargetSpec.ForDevice(Guid.CreateVersion7())], T0));
        Assert.Equal("target.all_devices_exclusive", ex.Code);
    }

    [Fact]
    public void Duplicate_targets_are_rejected()
    {
        var device = Guid.CreateVersion7();
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);

        var ex = Assert.Throws<DomainException>(() =>
            n.ReplaceTargets([NotificationTargetSpec.ForDevice(device), NotificationTargetSpec.ForDevice(device)], T0));
        Assert.Equal("target.duplicate", ex.Code);
    }

    [Theory]
    [InlineData(NotificationTargetType.Device)]
    [InlineData(NotificationTargetType.DeviceGroup)]
    [InlineData(NotificationTargetType.Recipient)]
    public void Typed_targets_require_reference(NotificationTargetType type)
    {
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);

        Assert.Throws<DomainException>(() => n.ReplaceTargets([new NotificationTargetSpec(type, null)], T0));
        Assert.Throws<DomainException>(() => n.ReplaceTargets([new NotificationTargetSpec(type, Guid.Empty)], T0));
    }

    [Fact]
    public void All_devices_target_rejects_reference()
    {
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);

        Assert.Throws<DomainException>(() =>
            n.ReplaceTargets([new NotificationTargetSpec(NotificationTargetType.AllDevices, Guid.CreateVersion7())], T0));
    }

    [Fact]
    public void Undefined_target_type_is_rejected()
    {
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);

        Assert.Throws<DomainException>(() => n.ReplaceTargets([new NotificationTargetSpec((NotificationTargetType)99, Guid.CreateVersion7())], T0));
    }

    [Fact]
    public void Target_count_is_bounded()
    {
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);
        var many = Enumerable.Range(0, Notification.MaxTargets + 1).Select(_ => NotificationTargetSpec.ForDevice(Guid.CreateVersion7()));

        Assert.Equal("target.too_many", Assert.Throws<DomainException>(() => n.ReplaceTargets(many, T0)).Code);
    }
}
