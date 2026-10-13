using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Health;

/// <summary>Unhealthy if the database schema is behind the application's migrations.</summary>
internal sealed class MigrationsHealthCheck(NotifyHubDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        return pending.Any()
            ? HealthCheckResult.Unhealthy("Database schema has pending migrations.")
            : HealthCheckResult.Healthy();
    }
}
