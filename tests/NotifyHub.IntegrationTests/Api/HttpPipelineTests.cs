using System.Net;
using System.Text.Json;
using NotifyHub.IntegrationTests.Infrastructure;

namespace NotifyHub.IntegrationTests.Api;

public sealed class HttpPipelineTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    [Fact]
    public async Task Valid_correlation_id_is_echoed()
    {
        await using var factory = new NotifyHubApiFactory(ConnectionString);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "abc-123_def.456");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("abc-123_def.456", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
    }

    [Theory]
    [InlineData("contains spaces")]
    [InlineData("<script>")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Malformed_correlation_id_is_replaced(string supplied)
    {
        await using var factory = new NotifyHubApiFactory(ConnectionString);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", supplied);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.NotEqual(supplied, echoed);
        Assert.Matches("^[A-Za-z0-9._-]{1,64}$", echoed);
    }

    [Fact]
    public async Task Correlation_id_is_generated_when_absent()
    {
        await using var factory = new NotifyHubApiFactory(ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(Assert.Single(response.Headers.GetValues("X-Correlation-ID"))));
    }

    [Fact]
    public async Task Security_headers_are_set_and_server_header_is_absent()
    {
        await using var factory = new NotifyHubApiFactory(ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Contains("default-src 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Server"));
    }

    [Theory]
    [InlineData("GET", "/api/v1/does-not-exist")]
    [InlineData("GET", "/api/v1/notifications")]
    [InlineData("POST", "/api/v1/tenants")]
    [InlineData("GET", "/")]
    public async Task Unauthenticated_requests_are_denied_by_default_with_problem_details(string method, string path)
    {
        await using var factory = new NotifyHubApiFactory(ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(401, body.GetProperty("status").GetInt32());
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Correlation-ID")), body.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task OpenApi_document_is_published()
    {
        await using var factory = new NotifyHubApiFactory(ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.StartsWith("3.", doc.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        Assert.Equal("NotifyHub API", doc.GetProperty("info").GetProperty("title").GetString());
        Assert.False(doc.TryGetProperty("servers", out var servers) && servers.GetArrayLength() > 0);
    }
}
