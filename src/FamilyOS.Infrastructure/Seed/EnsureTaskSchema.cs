using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

/// <summary>
/// Keeps the Tasks table compatible with the current TaskItem EF model on
/// installations whose migration history predates task scheduling/completion fields.
/// </summary>
public static class EnsureTaskSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE familyos."Tasks"
                ADD COLUMN IF NOT EXISTS "EstimatedMinutes" integer NULL,
                ADD COLUMN IF NOT EXISTS "CompletedAtUtc" timestamp with time zone NULL,
                ADD COLUMN IF NOT EXISTS "PlannedStartUtc" timestamp with time zone NULL,
                ADD COLUMN IF NOT EXISTS "PlannedEndUtc" timestamp with time zone NULL;
            """);

        logger.LogInformation("Task schema ensured (EstimatedMinutes, CompletedAtUtc, PlannedStartUtc, PlannedEndUtc)");
    }
}
