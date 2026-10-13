using Microsoft.EntityFrameworkCore;

namespace NotifyHub.Infrastructure.Persistence;

internal static class DbContextOptionsConfigurator
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString, int commandTimeoutSeconds)
    {
        options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(NotifyHubDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
                npgsql.CommandTimeout(commandTimeoutSeconds);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .UseSnakeCaseNamingConvention();
    }
}
