using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace NotifyHub.IntegrationTests.Api;

internal sealed class NotifyHubApiFactory(string connectionString, FakeTimeProvider? timeProvider = null) : WebApplicationFactory<NotifyHub.Api.ApiAssembly>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:NotifyHub", connectionString);
        builder.ConfigureTestServices(services =>
        {
            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(timeProvider);
            }
        });
    }
}
