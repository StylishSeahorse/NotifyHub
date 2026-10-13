using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NotifyHub.Application.Health;
using NotifyHub.IntegrationTests.Infrastructure;

namespace NotifyHub.IntegrationTests.Api;

public sealed class HealthEndpointTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    [Fact]
    public async Task Liveness_does_not_depend_on_the_database()
    {
        await using var factory = new NotifyHubApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", (await ReadAsync(response)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Readiness_is_healthy_with_current_schema_and_fresh_worker_heartbeat()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new NotifyHubApiFactory(ConnectionString, clock);
        await RecordHeartbeatAsync(factory, clock.GetUtcNow());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        var checks = body.GetProperty("checks");
        Assert.Equal("Healthy", checks.GetProperty("database").GetProperty("status").GetString());
        Assert.Equal("Healthy", checks.GetProperty("schema").GetProperty("status").GetString());
        Assert.Equal("Healthy", checks.GetProperty("worker").GetProperty("status").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Readiness_is_degraded_but_serving_when_worker_heartbeat_is_stale()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new NotifyHubApiFactory(ConnectionString, clock);
        await RecordHeartbeatAsync(factory, clock.GetUtcNow());
        clock.Advance(TimeSpan.FromMinutes(5));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", body.GetProperty("status").GetString());
        Assert.Equal("Degraded", body.GetProperty("checks").GetProperty("worker").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Readiness_is_unhealthy_when_schema_is_not_migrated()
    {
        var empty = await Postgres.CreateEmptyDatabaseAsync();
        await using var factory = new NotifyHubApiFactory(empty);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("schema").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Readiness_is_unhealthy_without_database_and_leaks_no_details()
    {
        const string secret = "s3cr3t-password-value";
        await using var factory = new NotifyHubApiFactory($"Host=127.0.0.1;Port=1;Database=none;Username=probe;Password={secret};Timeout=2");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", JsonDocument.Parse(raw).RootElement.GetProperty("checks").GetProperty("database").GetProperty("status").GetString());
        Assert.DoesNotContain(secret, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("description", raw, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RecordHeartbeatAsync(NotifyHubApiFactory factory, DateTimeOffset at)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatStore>();
        await store.RecordAsync(new WorkerHeartbeat(WorkerNames.Background, Guid.NewGuid(), "test", at, at), CancellationToken.None);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}
