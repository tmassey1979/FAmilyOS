using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsureRecipePreferenceSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS familyos."RecipeMemberPreferences" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "FamilyId" uuid NOT NULL,
                "RecipeId" uuid NOT NULL,
                "MemberId" uuid NOT NULL,
                "Preference" character varying(20) NULL,
                "IsFavorite" boolean NOT NULL DEFAULT false,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecipeMemberPreferences_FamilyId_RecipeId_MemberId"
                ON familyos."RecipeMemberPreferences" ("FamilyId", "RecipeId", "MemberId")
                WHERE "IsDeleted" = false;
            CREATE INDEX IF NOT EXISTS "IX_RecipeMemberPreferences_FamilyId_MemberId"
                ON familyos."RecipeMemberPreferences" ("FamilyId", "MemberId")
                WHERE "IsDeleted" = false;
            """);
        logger.LogInformation("Recipe member preference schema ensured");
    }
}
