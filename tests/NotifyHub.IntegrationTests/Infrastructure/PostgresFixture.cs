using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotifyHub.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(NotifyHub.IntegrationTests.Infrastructure.PostgresFixture))]

namespace NotifyHub.IntegrationTests.Infrastructure;

/// <summary>
/// Provides a real PostgreSQL server for the whole test assembly. Uses NOTIFYHUB_TEST_POSTGRES (a connection
/// string with CREATEDB rights) when set; otherwise starts a disposable container via Testcontainers.
/// Each test class creates its own freshly migrated database so tests never share state.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string DefaultImage = "postgres:17.11-alpine";

    private PostgreSqlContainer? _container;
    private string _adminConnectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("NOTIFYHUB_TEST_POSTGRES");
        if (!string.IsNullOrWhiteSpace(external))
        {
            _adminConnectionString = external;
            return;
        }

        _container = new PostgreSqlBuilder(Environment.GetEnvironmentVariable("NOTIFYHUB_TEST_POSTGRES_IMAGE") ?? DefaultImage)
            .Build();
        await _container.StartAsync();
        _adminConnectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Creates an empty database and returns its connection string.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = "nh_test_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = name, Pooling = true }.ConnectionString;
    }

    /// <summary>Creates a database and applies all migrations using the production migrator.</summary>
    public async Task<string> CreateMigratedDatabaseAsync()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        await using var db = CreateContext(connectionString);
        await new DatabaseMigrator(db, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(CancellationToken.None);
        return connectionString;
    }

    public static NotifyHubDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<NotifyHubDbContext>();
        DbContextOptionsConfigurator.Configure(builder, connectionString, 30);
        return new NotifyHubDbContext(builder.Options);
    }
}
