using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsureMealNominationSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS familyos."MealNominations" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "FamilyId" uuid NOT NULL,
                "WeekStartUtc" timestamp with time zone NOT NULL,
                "Title" character varying(300) NOT NULL,
                "RecipeId" uuid NULL,
                "ProposedByMemberId" uuid NOT NULL,
                "Note" character varying(1000) NULL,
                "Status" integer NOT NULL DEFAULT 0,
                "DecidedByMemberId" uuid NULL,
                "DecisionAtUtc" timestamp with time zone NULL,
                "PlannedDayUtc" timestamp with time zone NULL,
                "PlannedSlot" character varying(20) NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS "IX_MealNominations_FamilyId_WeekStartUtc"
                ON familyos."MealNominations" ("FamilyId", "WeekStartUtc");
            CREATE TABLE IF NOT EXISTS familyos."MealNominationVotes" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "FamilyId" uuid NOT NULL,
                "NominationId" uuid NOT NULL,
                "MemberId" uuid NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MealNominationVotes_NominationId_MemberId"
                ON familyos."MealNominationVotes" ("NominationId", "MemberId");
            CREATE INDEX IF NOT EXISTS "IX_MealNominationVotes_FamilyId"
                ON familyos."MealNominationVotes" ("FamilyId");
            """);
        logger.LogInformation("Ensured family meal nomination and voting schema.");
    }
}
