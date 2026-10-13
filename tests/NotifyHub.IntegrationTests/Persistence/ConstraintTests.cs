using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Auditing;
using NotifyHub.Domain.Devices;
using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Notifications;
using NotifyHub.Domain.Tenants;
using NotifyHub.IntegrationTests.Infrastructure;

namespace NotifyHub.IntegrationTests.Persistence;

public sealed class ConstraintTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private TenantSeed _a = null!;
    private TenantSeed _b = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _a = await SeedTenantAsync("alpha");
        _b = await SeedTenantAsync("bravo");
    }

    [Fact]
    public async Task Tenant_slug_is_unique()
    {
        await using var db = NewContext();
        db.Add(Tenant.Create("Another Alpha", "alpha", T0));

        await AssertPostgresErrorAsync(SqlStates.UniqueViolation, () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task User_identity_is_unique_per_provider_and_email_is_not()
    {
        await using var db = NewContext();
        db.Add(User.Create("https://idp.test/", "dup-subject", "shared@x.test", null, T0));
        db.Add(User.Create("https://other-idp.test/", "dup-subject", "shared@x.test", null, T0));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var db2 = NewContext();
        db2.Add(User.Create("https://idp.test/", "dup-subject", null, null, T0));
        await AssertPostgresErrorAsync(SqlStates.UniqueViolation, () => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_membership_is_unique_per_user()
    {
        await AssertPostgresErrorAsync(SqlStates.UniqueViolation, () => ExecuteAsync(
            "INSERT INTO tenant_memberships (tenant_id, user_id, role, status, created_at, updated_at) VALUES ($1, $2, 'Auditor', 'Active', now(), now())",
            _a.TenantId, _a.AdminId));
    }

    [Fact]
    public async Task Device_credential_identifier_is_globally_unique()
    {
        await using var db = NewContext();
        var keyA = await db.Devices.Where(d => d.Id == _a.DeviceId).Select(d => d.DeviceKeyId).SingleAsync(TestContext.Current.CancellationToken);
        var device = Device.Register(_b.TenantId, "copy", T0);
        device.CompleteEnrolment(keyA!, null, null, T0);
        db.Add(device);

        await AssertPostgresErrorAsync(SqlStates.UniqueViolation, () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Device_group_names_are_unique_per_tenant_case_insensitively()
    {
        await using var db = NewContext();
        db.Add(DeviceGroup.Create(_b.TenantId, "Reception", null, T0));
        db.Add(DeviceGroup.Create(_a.TenantId, "Reception", null, T0));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var db2 = NewContext();
        db2.Add(DeviceGroup.Create(_a.TenantId, "RECEPTION", null, T0));
        await AssertPostgresErrorAsync(SqlStates.UniqueViolation, () => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task One_recipient_state_per_recipient_and_notification()
    {
        var notification = await SeedPublishedNotificationAsync(_a, critical: false);
        const string sql =
            "INSERT INTO notification_recipient_states (tenant_id, notification_id, recipient_id, presentation_count, created_at, updated_at) VALUES ($1, $2, $3, 0, now(), now())";
        await ExecuteAsync(sql, _a.TenantId, notification, _a.RecipientAId);

        await AssertPostgresErrorAsync(SqlStates.UniqueViolation, () => ExecuteAsync(sql, _a.TenantId, notification, _a.RecipientAId));
        await ExecuteAsync(sql, _a.TenantId, notification, _a.RecipientBId);
    }

    [Fact]
    public async Task Event_idempotency_key_is_unique_per_device()
    {
        var notification = await SeedPublishedNotificationAsync(_a, critical: false);
        await using var db = NewContext();
        db.Add(NotificationEvent.Record(_a.TenantId, notification, _a.DeviceId, _a.RecipientAId, NotificationEventType.Dismissed, "evt-42", T0, T0));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var retry = NewContext();
        retry.Add(NotificationEvent.Record(_a.TenantId, notification, _a.DeviceId, _a.RecipientAId, NotificationEventType.Dismissed, "evt-42", T0, T0.AddSeconds(30)));
        await AssertPostgresErrorAsync(SqlStates.UniqueViolation, () => retry.SaveChangesAsync());

        await using var db3 = NewContext();
        Assert.Equal(1, await db3.NotificationEvents.CountAsync(e => e.IdempotencyKey == "evt-42", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delivery_is_unique_per_notification_device_and_recipient_including_null_recipient()
    {
        var notification = await SeedPublishedNotificationAsync(_a, critical: false);
        const string sql =
            "INSERT INTO notification_deliveries (id, tenant_id, notification_id, device_id, recipient_id, delivery_status, first_sent_at, last_sent_at, attempt_count) VALUES (gen_random_uuid(), $1, $2, $3, NULL, 'Sent', now(), now(), 1)";
        await ExecuteAsync(sql, _a.TenantId, notification, _a.DeviceId);

        await AssertPostgresErrorAsync(SqlStates.UniqueViolation, () => ExecuteAsync(sql, _a.TenantId, notification, _a.DeviceId));
    }

    [Fact]
    public async Task Target_reference_must_match_target_type()
    {
        var notification = await SeedPublishedNotificationAsync(_a, critical: false);

        await AssertPostgresErrorAsync(SqlStates.CheckViolation, () => ExecuteAsync(
            "INSERT INTO notification_targets (id, tenant_id, notification_id, target_type, device_group_id) VALUES (gen_random_uuid(), $1, $2, 'Device', $3)",
            _a.TenantId, notification, _a.GroupId));
        await AssertPostgresErrorAsync(SqlStates.CheckViolation, () => ExecuteAsync(
            "INSERT INTO notification_targets (id, tenant_id, notification_id, target_type, device_id) VALUES (gen_random_uuid(), $1, $2, 'AllDevices', $3)",
            _a.TenantId, notification, _a.DeviceId));
    }

    [Fact]
    public async Task Invalid_enum_values_and_inconsistent_lifecycle_rows_are_rejected()
    {
        var notification = await SeedPublishedNotificationAsync(_a, critical: false);

        await AssertPostgresErrorAsync(SqlStates.CheckViolation, () => ExecuteAsync("UPDATE notifications SET status = 'Deleted' WHERE id = $1", notification));
        await AssertPostgresErrorAsync(SqlStates.CheckViolation, () => ExecuteAsync("UPDATE notifications SET status = 'Withdrawn' WHERE id = $1", notification));
        await AssertPostgresErrorAsync(SqlStates.CheckViolation, () => ExecuteAsync("UPDATE notifications SET expires_at = starts_at - interval '1 minute' WHERE id = $1", notification));
        await AssertPostgresErrorAsync(SqlStates.CheckViolation, () => ExecuteAsync("UPDATE devices SET status = 'Revoked' WHERE id = $1", _a.DeviceId));
    }

    [Fact]
    public async Task Acknowledgement_columns_must_be_set_together()
    {
        var notification = await SeedPublishedNotificationAsync(_a, critical: true);

        await AssertPostgresErrorAsync(SqlStates.CheckViolation, () => ExecuteAsync(
            "INSERT INTO notification_recipient_states (tenant_id, notification_id, recipient_id, acknowledged_at, presentation_count, created_at, updated_at) VALUES ($1, $2, $3, now(), 0, now(), now())",
            _a.TenantId, notification, _a.RecipientAId));
    }

    [Fact]
    public async Task Audit_events_are_append_only()
    {
        await using var db = NewContext();
        var audit = AuditEvent.Record(_a.TenantId, AuditActorType.User, _a.AdminId, "notification.published", "notification", Guid.CreateVersion7(), T0);
        db.Add(audit);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await AssertPostgresErrorAsync(SqlStates.RestrictViolation, () => ExecuteAsync("UPDATE audit_events SET action = 'notification.tampered' WHERE id = $1", audit.Id));

        // Retention cleanup may delete.
        Assert.Equal(1, await ExecuteAsync("DELETE FROM audit_events WHERE id = $1", audit.Id));
    }

    [Fact]
    public async Task Notification_events_are_append_only()
    {
        var notification = await SeedPublishedNotificationAsync(_a, critical: false);
        await using var db = NewContext();
        var evt = NotificationEvent.Record(_a.TenantId, notification, _a.DeviceId, null, NotificationEventType.Displayed, "e1", T0, T0);
        db.Add(evt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await AssertPostgresErrorAsync(SqlStates.RestrictViolation, () => ExecuteAsync("UPDATE notification_events SET event_type = 'Acknowledged' WHERE id = $1", evt.Id));
    }

    [Fact]
    public async Task Targeted_device_cannot_be_deleted()
    {
        await using var db = NewContext();
        var n = Notification.CreateDraft(_a.TenantId, new NotificationContent("t", "b", NotificationSeverity.Info, DisplayMode.Toast, false), _a.AdminId, T0);
        n.ReplaceTargets([NotificationTargetSpec.ForDevice(_a.DeviceId)], T0);
        db.Add(n);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await AssertPostgresErrorAsync(SqlStates.ForeignKeyViolation, () => ExecuteAsync("DELETE FROM devices WHERE id = $1", _a.DeviceId));
    }
}
