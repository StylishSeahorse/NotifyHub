using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NotifyHub.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations. Run as an explicit deployment step (the "migrate" command) rather than
/// implicitly at application start-up, so that schema changes are deliberate and observable.
/// EF Core serialises concurrent migration runs with a database lock.
/// </summary>
public sealed class DatabaseMigrator(NotifyHubDbContext dbContext, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date");
            return;
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, pending);
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied");
    }
}
