using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NotifyHub.Application.Health;
using NotifyHub.Infrastructure;
using NotifyHub.IntegrationTests.Infrastructure;
using NotifyHub.Worker;

namespace NotifyHub.IntegrationTests.Worker;

public sealed class HeartbeatServiceTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    [Fact]
    public async Task Heartbeat_is_upserted_and_liveness_file_written()
    {
        var ct = TestContext.Current.CancellationToken;
        var livenessFile = Path.Combine(Path.GetTempPath(), $"nh-heartbeat-{Guid.NewGuid():N}");
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        await using var provider = BuildServices(clock);
        var service = new HeartbeatService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            Options.Create(new HeartbeatOptions { LivenessFilePath = livenessFile }),
            NullLogger<HeartbeatService>.Instance);

        try
        {
            var started = clock.GetUtcNow();
            await service.BeatAsync(started, ct);
            clock.Advance(TimeSpan.FromSeconds(30));
            await service.BeatAsync(started, ct);

            await using var scope = provider.CreateAsyncScope();
            var latest = await scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatStore>().GetLatestAsync(WorkerNames.Background, ct);
            Assert.NotNull(latest);
            Assert.Equal(started, latest.StartedAt);
            Assert.Equal(started.AddSeconds(30), latest.LastHeartbeatAt);
            Assert.Equal(1, await CountHeartbeatRowsAsync());
            Assert.True(File.Exists(livenessFile));
        }
        finally
        {
            File.Delete(livenessFile);
        }
    }

    [Fact]
    public async Task Heartbeat_failure_is_contained()
    {
        var clock = new FakeTimeProvider();
        await using var provider = BuildServices(clock, "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1");
        var service = new HeartbeatService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            Options.Create(new HeartbeatOptions()),
            NullLogger<HeartbeatService>.Instance);

        var ex = await Record.ExceptionAsync(() => service.BeatAsync(clock.GetUtcNow(), TestContext.Current.CancellationToken));

        Assert.Null(ex);
    }

    private ServiceProvider BuildServices(TimeProvider clock, string? connectionString = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:NotifyHub"] = connectionString ?? ConnectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifyHubInfrastructure(configuration);
        services.AddSingleton(clock);
        return services.BuildServiceProvider();
    }

    private async Task<long> CountHeartbeatRowsAsync()
    {
        await using var db = NewContext();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.LongCountAsync(db.WorkerHeartbeats, TestContext.Current.CancellationToken);
    }
}
