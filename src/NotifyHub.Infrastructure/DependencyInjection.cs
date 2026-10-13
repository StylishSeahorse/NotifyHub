using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotifyHub.Application.Health;
using NotifyHub.Infrastructure.Health;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure;

public static class DependencyInjection
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddNotifyHubInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{DatabaseOptions.ConnectionStringName}' is not configured. " +
                "Set it through configuration or the ConnectionStrings__NotifyHub environment variable.");
        }

        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<WorkerHealthOptions>(configuration.GetSection("Health:Worker"));
        var commandTimeout = configuration.GetValue("Database:CommandTimeoutSeconds", 30);

        services.AddDbContext<NotifyHubDbContext>(options =>
            DbContextOptionsConfigurator.Configure(options, connectionString, commandTimeout));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<IWorkerHeartbeatStore, WorkerHeartbeatStore>();

        return services;
    }

    /// <summary>Readiness checks: database reachable, schema current, background worker alive.</summary>
    public static IHealthChecksBuilder AddNotifyHubHealthChecks(this IServiceCollection services)
    {
        return services.AddHealthChecks()
            .AddDbContextCheck<NotifyHubDbContext>("database", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<MigrationsHealthCheck>("schema", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<WorkerHeartbeatHealthCheck>("worker", HealthStatus.Degraded, [ReadyTag]);
    }
}
