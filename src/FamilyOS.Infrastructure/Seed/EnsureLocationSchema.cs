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
                CREATE TABLE IF NOT EXISTS familyos."MemberLocationViewerGrants" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "SharingMemberId" uuid NOT NULL,
                    "ViewerMemberId" uuid NOT NULL,
                    "GrantedAtUtc" timestamp with time zone NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MemberLocationViewerGrants_SharingMemberId_ViewerMemberId"
                    ON familyos."MemberLocationViewerGrants" ("SharingMemberId", "ViewerMemberId");
                CREATE INDEX IF NOT EXISTS "IX_MemberLocationViewerGrants_FamilyId_ViewerMemberId"
                    ON familyos."MemberLocationViewerGrants" ("FamilyId", "ViewerMemberId");
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
