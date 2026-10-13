using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Devices;
using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Notifications;
using NotifyHub.Domain.Tenants;
using NotifyHub.IntegrationTests.Infrastructure;

namespace NotifyHub.IntegrationTests.Persistence;

/// <summary>
/// The database itself must refuse cross-tenant references, even if application code is bypassed or buggy.
/// These tests deliberately write rows that the domain layer would reject.
/// </summary>
public sealed class TenantIsolationConstraintTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private TenantSeed _a = null!;
    private TenantSeed _b = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _a = await SeedTenantAsync("tenant-a");
        _b = await SeedTenantAsync("tenant-b");
    }

    [Fact]
    public async Task Device_cannot_join_another_tenants_group()
    {
        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync(
            "INSERT INTO device_group_members (tenant_id, device_group_id, device_id, added_at) VALUES ($1, $2, $3, now())",
            _a.TenantId, _a.GroupId, _b.DeviceId));

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync(
            "INSERT INTO device_group_members (tenant_id, device_group_id, device_id, added_at) VALUES ($1, $2, $3, now())",
            _b.TenantId, _a.GroupId, _b.DeviceId));
    }

    [Theory]
    [InlineData(NotificationTargetType.Device)]
    [InlineData(NotificationTargetType.DeviceGroup)]
    [InlineData(NotificationTargetType.Recipient)]
    public async Task Notification_cannot_target_another_tenants_entities(NotificationTargetType type)
    {
        var foreignId = type switch
        {
            NotificationTargetType.Device => _b.DeviceId,
            NotificationTargetType.DeviceGroup => _b.GroupId,
            _ => _b.RecipientAId,
        };

        await using var db = NewContext();
        var n = Notification.CreateDraft(_a.TenantId, new NotificationContent("t", "b", NotificationSeverity.Info, DisplayMode.Toast, false), _a.AdminId, T0);
        n.ReplaceTargets([new NotificationTargetSpec(type, foreignId)], T0);
        db.Add(n);

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Recipient_state_cannot_link_another_tenants_recipient_or_notification()
    {
        var notificationA = await SeedPublishedNotificationAsync(_a, critical: false);
        const string sql =
            "INSERT INTO notification_recipient_states (tenant_id, notification_id, recipient_id, presentation_count, created_at, updated_at) VALUES ($1, $2, $3, 0, now(), now())";

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync(sql, _a.TenantId, notificationA, _b.RecipientAId));
        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync(sql, _b.TenantId, notificationA, _b.RecipientAId));
    }

    [Fact]
    public async Task Event_cannot_reference_another_tenants_device()
    {
        var notificationA = await SeedPublishedNotificationAsync(_a, critical: false);

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync(
            "INSERT INTO notification_events (id, tenant_id, notification_id, device_id, event_type, occurred_at, received_at, idempotency_key) VALUES (gen_random_uuid(), $1, $2, $3, 'Displayed', now(), now(), 'k1')",
            _a.TenantId, notificationA, _b.DeviceId));
    }

    [Fact]
    public async Task Delivery_cannot_reference_another_tenants_device()
    {
        var notificationA = await SeedPublishedNotificationAsync(_a, critical: false);

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync(
            "INSERT INTO notification_deliveries (id, tenant_id, notification_id, device_id, delivery_status, first_sent_at, last_sent_at, attempt_count) VALUES (gen_random_uuid(), $1, $2, $3, 'Sent', now(), now(), 1)",
            _a.TenantId, notificationA, _b.DeviceId));
    }

    [Fact]
    public async Task Branding_cannot_use_another_tenants_asset()
    {
        var assetB = Guid.CreateVersion7();
        await ExecuteAsync(
            "INSERT INTO assets (id, tenant_id, storage_key, mime_type, size_bytes, sha256, created_at) VALUES ($1, $2, $3, 'image/png', 10, repeat('a', 64), now())",
            assetB, _b.TenantId, $"{_b.TenantId:N}/{assetB:N}");

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync(
            "INSERT INTO tenant_branding (tenant_id, logo_asset_id, theme_json, branding_version, updated_at) VALUES ($1, $2, '{}', 1, now())",
            _a.TenantId, assetB));
    }

    [Fact]
    public async Task Local_account_recipient_cannot_reference_another_tenants_device()
    {
        await using var db = NewContext();
        db.Add(Recipient.ForLocalAccount(_a.TenantId, _b.DeviceId, "S-1-5-21-1-2-3-1001", null, T0));

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Revision_cannot_supersede_another_tenants_notification()
    {
        var notificationA = await SeedPublishedNotificationAsync(_a, critical: false);
        var notificationB = await SeedPublishedNotificationAsync(_b, critical: false);

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync(
            "UPDATE notifications SET superseded_by_notification_id = $1 WHERE id = $2",
            notificationB, notificationA));
    }

    [Fact]
    public async Task Same_external_identity_may_exist_independently_in_two_tenants()
    {
        await using var db = NewContext();
        var aliceInA = await db.Recipients.SingleAsync(r => r.Id == _a.RecipientAId, TestContext.Current.CancellationToken);
        var aliceInB = await db.Recipients.SingleAsync(r => r.Id == _b.RecipientAId, TestContext.Current.CancellationToken);

        Assert.Equal(aliceInA.Subject, aliceInB.Subject);
        Assert.NotEqual(aliceInA.Id, aliceInB.Id);
    }

    [Fact]
    public async Task Same_tenant_references_are_accepted()
    {
        await using var db = NewContext();
        var n = Notification.CreateDraft(_a.TenantId, new NotificationContent("t", "b", NotificationSeverity.Info, DisplayMode.Toast, false), _a.AdminId, T0);
        n.ReplaceTargets([NotificationTargetSpec.ForDevice(_a.DeviceId), NotificationTargetSpec.Group(_a.GroupId), NotificationTargetSpec.ForRecipient(_a.RecipientAId)], T0);
        db.Add(n);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
