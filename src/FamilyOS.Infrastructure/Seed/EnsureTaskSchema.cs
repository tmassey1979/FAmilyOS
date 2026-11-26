using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

/// <summary>
/// Keeps the Tasks table compatible with the current TaskItem EF model on
/// installations whose migration history predates CompletedAtUtc.
/// </summary>
public static class EnsureTaskSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE familyos."Tasks"
                ADD COLUMN IF NOT EXISTS "CompletedAtUtc" timestamp with time zone NULL;
            """);

        logger.LogInformation("Task schema ensured (CompletedAtUtc)");
    }
}
