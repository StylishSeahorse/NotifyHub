using Microsoft.EntityFrameworkCore;
using NotifyHub.Application.Health;

namespace NotifyHub.Infrastructure.Persistence;

internal sealed class WorkerHeartbeatStore(NotifyHubDbContext dbContext) : IWorkerHeartbeatStore
{
    public async Task RecordAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        // Single-statement upsert: safe under concurrent instances and restarts.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO worker_heartbeats (worker_name, instance_id, version, started_at, last_heartbeat_at)
            VALUES ({heartbeat.WorkerName}, {heartbeat.InstanceId}, {heartbeat.Version}, {heartbeat.StartedAt.ToUniversalTime()}, {heartbeat.LastHeartbeatAt.ToUniversalTime()})
            ON CONFLICT (worker_name, instance_id)
            DO UPDATE SET last_heartbeat_at = EXCLUDED.last_heartbeat_at, version = EXCLUDED.version
            """,
            cancellationToken);
    }

    public async Task<WorkerHeartbeat?> GetLatestAsync(string workerName, CancellationToken cancellationToken)
    {
        var record = await dbContext.WorkerHeartbeats
            .AsNoTracking()
            .Where(x => x.WorkerName == workerName)
            .OrderByDescending(x => x.LastHeartbeatAt)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null
            ? null
            : new WorkerHeartbeat(record.WorkerName, record.InstanceId, record.Version, record.StartedAt, record.LastHeartbeatAt);
    }
}
