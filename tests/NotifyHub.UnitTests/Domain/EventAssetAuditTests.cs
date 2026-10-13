using NotifyHub.Domain.Assets;
using NotifyHub.Domain.Auditing;
using NotifyHub.Domain.Common;
using NotifyHub.Domain.Notifications;
using static NotifyHub.UnitTests.Domain.TestData;

namespace NotifyHub.UnitTests.Domain;

public sealed class EventAssetAuditTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Event_records_reported_and_received_times()
    {
        var e = NotificationEvent.Record(TenantA, Guid.CreateVersion7(), DeviceId, null, NotificationEventType.Displayed, "evt-0001", T0, T0.AddSeconds(3));

        Assert.Equal(T0, e.OccurredAt);
        Assert.Equal(T0.AddSeconds(3), e.ReceivedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("../../etc/passwd")]
    [InlineData("key\nnewline")]
    public void Malformed_idempotency_keys_are_rejected(string key)
    {
        Assert.Throws<DomainException>(() =>
            NotificationEvent.Record(TenantA, Guid.CreateVersion7(), DeviceId, null, NotificationEventType.Displayed, key, T0, T0));
    }

    [Fact]
    public void Oversized_event_metadata_is_rejected()
    {
        Assert.Throws<DomainException>(() =>
            NotificationEvent.Record(TenantA, Guid.CreateVersion7(), DeviceId, null, NotificationEventType.Displayed, "k", T0, T0, new string('x', NotificationEvent.MetadataMaxLength + 1)));
    }

    [Fact]
    public void Asset_storage_key_is_derived_from_server_ids_only()
    {
        var asset = Asset.Create(TenantA, "image/png", 1024, Hash, Admin, T0);

        Assert.Equal($"{TenantA:N}/{asset.Id:N}", asset.StorageKey);
        Assert.DoesNotContain("..", asset.StorageKey, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("image/svg+xml", 1024)]
    [InlineData("text/html", 1024)]
    [InlineData("application/octet-stream", 1024)]
    [InlineData("image/png", 0)]
    [InlineData("image/png", Asset.MaxSizeBytes + 1)]
    public void Unsafe_or_oversized_assets_are_rejected(string mime, long size)
    {
        Assert.Throws<DomainException>(() => Asset.Create(TenantA, mime, size, Hash, Admin, T0));
    }

    [Fact]
    public void Asset_hash_must_be_lowercase_sha256()
    {
        Assert.Throws<DomainException>(() => Asset.Create(TenantA, "image/png", 10, Hash.ToUpperInvariant(), Admin, T0));
    }

    [Fact]
    public void Audit_event_requires_actor_unless_system()
    {
        Assert.Throws<DomainException>(() => AuditEvent.Record(TenantA, AuditActorType.User, null, "notification.published", "notification", null, T0));
        var system = AuditEvent.Record(null, AuditActorType.System, null, "notification.expired", "notification", Guid.CreateVersion7(), T0);
        Assert.Null(system.TenantId);
    }

    [Theory]
    [InlineData("Published")]
    [InlineData("notification")]
    [InlineData("notification published")]
    public void Audit_action_must_be_dotted_identifier(string action)
    {
        Assert.Throws<DomainException>(() => AuditEvent.Record(TenantA, AuditActorType.System, null, action, "notification", null, T0));
    }
}
