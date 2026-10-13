using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotifyHub.Contracts.V1;
using NotifyHub.Infrastructure;

namespace NotifyHub.Api.Health;

internal static class HealthEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// /health/live — process is running (no dependencies; used for container restarts).
    /// /health/ready — database reachable, schema current, worker heartbeat fresh (used for routing traffic).
    /// Responses contain only statuses and timings, never exception messages or configuration.
    /// </summary>
    public static void MapNotifyHubHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteAsync,
        }).AllowAnonymous().DisableHttpMetrics();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DependencyInjection.ReadyTag),
            ResponseWriter = WriteAsync,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
        }).AllowAnonymous().DisableHttpMetrics();
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var response = new HealthResponse(
            report.Status.ToString(),
            report.Entries.ToDictionary(
                e => e.Key,
                e => new HealthEntryResponse(e.Value.Status.ToString(), Math.Round(e.Value.Duration.TotalMilliseconds, 1))));

        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
