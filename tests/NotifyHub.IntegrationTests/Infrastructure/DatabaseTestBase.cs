using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Devices;
using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Notifications;
using NotifyHub.Domain.Tenants;
using NotifyHub.Infrastructure.Persistence;
using Npgsql;

namespace NotifyHub.IntegrationTests.Infrastructure;

/// <summary>Gives each test class its own migrated database and helpers to seed tenant-scoped data.</summary>
public abstract class DatabaseTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected static readonly DateTimeOffset T0 = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
    protected const string DomainSid = "S-1-5-21-1004336348-1177238915-682003330";

    protected PostgresFixture Postgres { get; } = postgres;

    protected string ConnectionString { get; private set; } = string.Empty;

    public virtual async ValueTask InitializeAsync()
    {
        ConnectionString = await Postgres.CreateMigratedDatabaseAsync();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected NotifyHubDbContext NewContext() => PostgresFixture.CreateContext(ConnectionString);

    protected async Task<TenantSeed> SeedTenantAsync(string slug)
    {
        await using var db = NewContext();
        var tenant = Tenant.Create(slug, slug, T0);
        var admin = User.Create("https://idp.test/", $"{slug}-admin-{Guid.NewGuid():N}", $"admin@{slug}.test", null, T0);
        var device = Device.Register(tenant.Id, $"{slug}-PC-01", T0);
        device.CompleteEnrolment($"key-{Guid.NewGuid():N}", "Windows 11", "0.1.0", T0);
        var group = DeviceGroup.Create(tenant.Id, "Front desk", null, T0);
        var recipientA = Recipient.ForActiveDirectory(tenant.Id, DomainSid, $"{DomainSid}-1001", "user-a", T0);
        var recipientB = Recipient.ForActiveDirectory(tenant.Id, DomainSid, $"{DomainSid}-1002", "user-b", T0);

        db.AddRange(tenant, admin, device, group, recipientA, recipientB);
        db.Add(TenantMembership.Create(tenant.Id, admin.Id, TenantRole.TenantAdministrator, T0));
        db.Add(group.AddMember(device, T0));
        await db.SaveChangesAsync();
        return new TenantSeed(tenant.Id, admin.Id, device.Id, group.Id, recipientA.Id, recipientB.Id);
    }

    protected async Task<Guid> SeedPublishedNotificationAsync(TenantSeed seed, bool critical)
    {
        await using var db = NewContext();
        var content = critical
            ? new NotificationContent("Critical", "Acknowledge this.", NotificationSeverity.Critical, DisplayMode.Popup, true)
            : new NotificationContent("Info", "Read this.", NotificationSeverity.Info, DisplayMode.Toast, false);
        var n = Notification.CreateDraft(seed.TenantId, content, seed.AdminId, T0);
        n.ReplaceTargets([NotificationTargetSpec.Group(seed.GroupId)], T0);
        n.PublishNow(seed.AdminId, T0);
        db.Add(n);
        await db.SaveChangesAsync();
        return n.Id;
    }

    protected async Task<int> ExecuteAsync(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var p in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = p });
        }

        return await command.ExecuteNonQueryAsync();
    }

    protected static async Task<PostgresException> AssertPostgresErrorAsync(string expectedSqlState, Func<Task> action)
    {
        var ex = await Record.ExceptionAsync(action);
        var pg = ex switch
        {
            PostgresException p => p,
            DbUpdateException { InnerException: PostgresException p } => p,
            _ => null,
        };
        Assert.True(pg is not null, $"Expected PostgreSQL error {expectedSqlState} but got: {ex?.GetType().Name ?? "no exception"} {ex?.Message}");
        Assert.Equal(expectedSqlState, pg!.SqlState);
        return pg;
    }

    protected sealed record TenantSeed(Guid TenantId, Guid AdminId, Guid DeviceId, Guid GroupId, Guid RecipientAId, Guid RecipientBId);
}

internal static class SqlStates
{
    public const string UniqueViolation = "23505";
    public const string ForeignKeyViolation = "23503";
    public const string CheckViolation = "23514";
    public const string RestrictViolation = "23001";
}
