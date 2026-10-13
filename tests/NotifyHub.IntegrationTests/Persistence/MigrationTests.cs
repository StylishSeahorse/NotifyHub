using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NotifyHub.IntegrationTests.Infrastructure;
using Npgsql;

namespace NotifyHub.IntegrationTests.Persistence;

public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_apply_to_an_empty_database_and_match_the_model()
    {
        var cs = await postgres.CreateMigratedDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(cs);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.False(db.Database.HasPendingModelChanges(), "The EF model has changes that are not captured in a migration.");

        var tables = await ListTablesAsync(cs);
        foreach (var expected in new[]
        {
            "tenants", "tenant_branding", "users", "platform_administrators", "tenant_memberships", "recipients",
            "devices", "device_groups", "device_group_members", "notifications", "notification_targets",
            "notification_recipient_states", "notification_deliveries", "notification_events", "assets",
            "audit_events", "worker_heartbeats",
        })
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task Migrating_twice_is_a_no_op()
    {
        var cs = await postgres.CreateMigratedDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(cs);

        await new NotifyHub.Infrastructure.Persistence.DatabaseMigrator(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<NotifyHub.Infrastructure.Persistence.DatabaseMigrator>.Instance)
            .MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Down_migrations_revert_cleanly_and_can_be_reapplied()
    {
        var cs = await postgres.CreateMigratedDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(cs);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(Migration.InitialDatabase, cancellationToken: TestContext.Current.CancellationToken);
        var remaining = await ListTablesAsync(cs);
        Assert.Equal(["__ef_migrations_history"], remaining);

        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("notifications", await ListTablesAsync(cs));
    }

    private static async Task<List<string>> ListTablesAsync(string cs)
    {
        await using var connection = new NpgsqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY 1", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}
