using System.Reflection;
using NotifyHub.Application.Health;

namespace NotifyHub.Worker;

public sealed class HeartbeatOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// File touched after each successful heartbeat; the container health check tests its age. Leave empty to
    /// disable (for example when running outside a container).
    /// </summary>
    public string? LivenessFilePath { get; set; }
}

/// <summary>
/// Records this worker instance's liveness in the database (read by the API's readiness check) and,
/// optionally, on the local filesystem (read by the container health check). A failed heartbeat is logged
/// and retried on the next tick; it never crashes the host.
/// </summary>
internal sealed class HeartbeatService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    Microsoft.Extensions.Options.IOptions<HeartbeatOptions> options,
    ILogger<HeartbeatService> logger) : BackgroundService
{
    private static readonly string Version =
        typeof(HeartbeatService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private readonly Guid _instanceId = Guid.CreateVersion7();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        logger.LogInformation("Background worker instance {InstanceId} starting (version {Version})", _instanceId, Version);

        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        do
        {
            await BeatAsync(startedAt, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task BeatAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatStore>();
            await store.RecordAsync(
                new WorkerHeartbeat(WorkerNames.Background, _instanceId, Version, startedAt, timeProvider.GetUtcNow()),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(options.Value.LivenessFilePath))
            {
                await File.WriteAllTextAsync(options.Value.LivenessFilePath, timeProvider.GetUtcNow().ToString("O"), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Heartbeat failed; will retry on next interval");
        }
    }
}
