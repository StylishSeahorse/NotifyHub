using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NotifyHub.Application.Health;

namespace NotifyHub.Infrastructure.Health;

public sealed class WorkerHealthOptions
{
    /// <summary>A heartbeat older than this marks the background worker as degraded.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Reports the background worker's liveness from its last database heartbeat. A stale or missing heartbeat is
/// Degraded rather than Unhealthy: the API can still serve requests, but scheduling and expiry are delayed.
/// </summary>
internal sealed class WorkerHeartbeatHealthCheck(IWorkerHeartbeatStore store, TimeProvider timeProvider, IOptions<WorkerHealthOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var latest = await store.GetLatestAsync(WorkerNames.Background, cancellationToken);
        if (latest is null)
        {
            return HealthCheckResult.Degraded("No background worker heartbeat recorded.");
        }

        var age = timeProvider.GetUtcNow() - latest.LastHeartbeatAt;
        return age > options.Value.StaleAfter
            ? HealthCheckResult.Degraded("Background worker heartbeat is stale.")
            : HealthCheckResult.Healthy();
    }
}
