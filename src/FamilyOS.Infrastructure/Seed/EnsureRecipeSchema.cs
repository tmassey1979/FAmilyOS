using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsureRecipeSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS familyos."Recipes" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "Name" character varying(300) NOT NULL,
                    "Servings" integer NOT NULL DEFAULT 4,
                    "IngredientsCsv" character varying(2000) NULL,
                    "Instructions" character varying(4000) NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );
                ALTER TABLE familyos."Recipes" ADD COLUMN IF NOT EXISTS "Description" character varying(1000) NULL;
                ALTER TABLE familyos."Recipes" ADD COLUMN IF NOT EXISTS "ImageUrl" character varying(1000) NULL;
                ALTER TABLE familyos."Recipes" ADD COLUMN IF NOT EXISTS "SourceUrl" character varying(1000) NULL;
                ALTER TABLE familyos."Recipes" ADD COLUMN IF NOT EXISTS "PrepMinutes" integer NULL;
                ALTER TABLE familyos."Recipes" ADD COLUMN IF NOT EXISTS "CookMinutes" integer NULL;
                ALTER TABLE familyos."Recipes" ADD COLUMN IF NOT EXISTS "Cuisine" character varying(100) NULL;
                ALTER TABLE familyos."Recipes" ADD COLUMN IF NOT EXISTS "Difficulty" character varying(40) NULL;
                CREATE INDEX IF NOT EXISTS "IX_Recipes_FamilyId_Name"
                    ON familyos."Recipes" ("FamilyId", "Name");
                """);
            logger.LogInformation("Recipe schema ensured");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnsureRecipeSchema failed");
            throw;
        }
    }
}
