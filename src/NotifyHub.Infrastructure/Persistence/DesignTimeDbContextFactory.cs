using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NotifyHub.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool to generate migrations. Generating migrations does not connect to a
/// database; the placeholder connection string is never used at runtime.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NotifyHubDbContext>
{
    public NotifyHubDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("NOTIFYHUB_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=notifyhub_design;Username=notifyhub";
        var builder = new DbContextOptionsBuilder<NotifyHubDbContext>();
        DbContextOptionsConfigurator.Configure(builder, connectionString, 30);
        return new NotifyHubDbContext(builder.Options);
    }
}
