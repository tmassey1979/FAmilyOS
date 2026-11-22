using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsureLocationSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS familyos."MemberLocations" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "MemberId" uuid NOT NULL,
                    "SharingEnabled" boolean NOT NULL DEFAULT false,
                    "ConsentUpdatedAtUtc" timestamp with time zone NULL,
                    "Latitude" double precision NULL,
                    "Longitude" double precision NULL,
                    "AccuracyMeters" double precision NULL,
                    "LocationUpdatedAtUtc" timestamp with time zone NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MemberLocations_MemberId"
                    ON familyos."MemberLocations" ("MemberId");
                CREATE INDEX IF NOT EXISTS "IX_MemberLocations_FamilyId_SharingEnabled"
                    ON familyos."MemberLocations" ("FamilyId", "SharingEnabled");
                """);
            logger.LogInformation("Member location schema ensured");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not ensure member location schema");
            throw;
        }
    }
}
