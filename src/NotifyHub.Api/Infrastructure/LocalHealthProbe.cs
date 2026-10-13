namespace NotifyHub.Api.Infrastructure;

/// <summary>
/// Implements the "healthcheck" command used by the container health check (runtime images ship without
/// curl). Calls the local liveness endpoint and returns a process exit code.
/// </summary>
internal static class LocalHealthProbe
{
    public static async Task<int> RunAsync()
    {
        var ports = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080";
        var port = ports.Split(';', ',')[0].Trim();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/health/live"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
