using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsurePreferredGroceryStoreSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS familyos."PreferredGroceryStores" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "FamilyId" uuid NOT NULL,
                "ProviderKey" character varying(40) NOT NULL,
                "ProviderLocationId" character varying(120) NOT NULL,
                "StoreName" character varying(200) NOT NULL,
                "Address" character varying(300) NOT NULL,
                "City" character varying(120) NOT NULL DEFAULT '',
                "Region" character varying(80) NOT NULL DEFAULT '',
                "PostalCode" character varying(20) NOT NULL DEFAULT '',
                "Latitude" double precision NULL,
                "Longitude" double precision NULL,
                "IsDemo" boolean NOT NULL DEFAULT true,
                "VerifiedAtUtc" timestamp with time zone NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PreferredGroceryStores_FamilyId"
                ON familyos."PreferredGroceryStores" ("FamilyId") WHERE "IsDeleted" = false;
            """);
        logger.LogInformation("Ensured preferred grocery store schema.");
    }
}
