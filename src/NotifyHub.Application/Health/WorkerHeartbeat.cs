namespace NotifyHub.Application.Health;

/// <summary>Liveness record written periodically by each background worker instance.</summary>
public sealed record WorkerHeartbeat(
    string WorkerName,
    Guid InstanceId,
    string Version,
    DateTimeOffset StartedAt,
    DateTimeOffset LastHeartbeatAt);

public interface IWorkerHeartbeatStore
{
    Task RecordAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken);

    /// <summary>Returns the most recent heartbeat across all instances of <paramref name="workerName"/>, if any.</summary>
    Task<WorkerHeartbeat?> GetLatestAsync(string workerName, CancellationToken cancellationToken);
}

public static class WorkerNames
{
    public const string Background = "background-worker";
}
