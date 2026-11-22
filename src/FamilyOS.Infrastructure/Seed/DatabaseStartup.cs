using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

/// <summary>
/// Applies EF migrations before the API begins serving traffic. Existing databases
/// without migration history are never silently baselined or overwritten.
/// </summary>
public static class DatabaseStartup
{
    public static async Task RunAsync(
        FamilyOsDbContext db,
        ILogger logger,
        bool applyMigrations,
        CancellationToken cancellationToken = default)
    {
        await db.Database.CanConnectAsync(cancellationToken);

        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation(
                "Database migration check passed. AppliedMigrations={AppliedCount}",
                applied.Length);
            return;
        }

        if (!applyMigrations)
        {
            throw new InvalidOperationException(
                $"Database has {pending.Length} pending EF migration(s), but Database:MigrateOnStartup is disabled. " +
                "Apply migrations explicitly before starting the API.");
        }

        // An empty migration history plus existing application tables indicates a
        // legacy/unbaselined database. Applying InitialCreate there could destroy or
        // collide with live schema, so require an intentional baseline operation.
        if (applied.Length == 0 && await HasExistingApplicationSchemaAsync(db, cancellationToken))
        {
            throw new InvalidOperationException(
                "The familyos schema already contains application tables, but EF migration history is empty. " +
                "Refusing to apply InitialCreate to an existing database. Baseline and verify the database " +
                "against the migration snapshot before enabling automatic migrations.");
        }

        logger.LogInformation(
            "Applying database migrations. Applied={AppliedCount} Pending={PendingCount}",
            applied.Length, pending.Length);

        await db.Database.MigrateAsync(cancellationToken);

        var remaining = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (remaining.Length > 0)
        {
            throw new InvalidOperationException(
                $"Database migration completed with {remaining.Length} migration(s) still pending.");
        }

        logger.LogInformation("Database migrations applied successfully.");
    }

    private static async Task<bool> HasExistingApplicationSchemaAsync(
        FamilyOsDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != System.Data.ConnectionState.Open;
        if (closeAfter)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass('familyos.\\"Users\\"') IS NOT NULL;";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is bool exists && exists;
        }
        finally
        {
            if (closeAfter)
                await connection.CloseAsync();
        }
    }
}
