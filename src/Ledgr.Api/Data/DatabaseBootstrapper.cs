using Microsoft.EntityFrameworkCore;

namespace Ledgr.Api.Data;

public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var options = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (options.MigrateOnStartup)
        {
            await database.Database.MigrateAsync(cancellationToken);
        }
        else if (!await database.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The database is not reachable. Apply migrations before starting the application, " +
                "or set Database:MigrateOnStartup to true in development.");
        }

        if (options.Seed)
        {
            await DatabaseSeeder.SeedAsync(database, cancellationToken);
        }
    }
}
