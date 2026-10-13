namespace NotifyHub.Infrastructure.Persistence;

internal sealed class WorkerHeartbeatRecord
{
    public required string WorkerName { get; set; }

    public required Guid InstanceId { get; set; }

    public required string Version { get; set; }

    public required DateTimeOffset StartedAt { get; set; }

    public required DateTimeOffset LastHeartbeatAt { get; set; }
}
