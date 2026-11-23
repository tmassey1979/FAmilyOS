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
                CREATE TABLE IF NOT EXISTS familyos."CheckInRequests" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "RequesterMemberId" uuid NOT NULL,
                    "TargetMemberId" uuid NOT NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "DueAtUtc" timestamp with time zone NOT NULL,
                    "Status" character varying(20) NOT NULL,
                    "RespondedAtUtc" timestamp with time zone NULL,
                    "ResponseMessage" character varying(500) NULL,
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );
                CREATE INDEX IF NOT EXISTS "IX_CheckInRequests_FamilyId_TargetMemberId_Status_DueAtUtc"
                    ON familyos."CheckInRequests" ("FamilyId", "TargetMemberId", "Status", "DueAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_CheckInRequests_FamilyId_RequesterMemberId_CreatedAtUtc"
                    ON familyos."CheckInRequests" ("FamilyId", "RequesterMemberId", "CreatedAtUtc");
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
                ALTER TABLE familyos."MemberLocations"
                    ADD COLUMN IF NOT EXISTS "HistoryEnabled" boolean NOT NULL DEFAULT false;
                ALTER TABLE familyos."MemberLocations" ADD COLUMN IF NOT EXISTS "BatterySharingEnabled" boolean NOT NULL DEFAULT false;
                ALTER TABLE familyos."MemberLocations" ADD COLUMN IF NOT EXISTS "BatteryPercent" integer NULL;
                ALTER TABLE familyos."MemberLocations" ADD COLUMN IF NOT EXISTS "IsCharging" boolean NULL;
                ALTER TABLE familyos."MemberLocations" ADD COLUMN IF NOT EXISTS "BatteryUpdatedAtUtc" timestamp with time zone NULL;
                CREATE TABLE IF NOT EXISTS familyos."MemberLocationHistory" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "MemberId" uuid NOT NULL,
                    "Latitude" double precision NOT NULL,
                    "Longitude" double precision NOT NULL,
                    "AccuracyMeters" double precision NOT NULL,
                    "RecordedAtUtc" timestamp with time zone NOT NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );
                CREATE INDEX IF NOT EXISTS "IX_MemberLocationHistory_MemberId_RecordedAtUtc"
                    ON familyos."MemberLocationHistory" ("MemberId", "RecordedAtUtc");
                CREATE INDEX IF NOT EXISTS "IX_MemberLocationHistory_FamilyId_MemberId_RecordedAtUtc"
                    ON familyos."MemberLocationHistory" ("FamilyId", "MemberId", "RecordedAtUtc");
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
                CREATE TABLE IF NOT EXISTS familyos."SavedPlaces" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "CreatedByMemberId" uuid NOT NULL,
                    "Name" character varying(120) NOT NULL,
                    "Latitude" double precision NOT NULL,
                    "Longitude" double precision NOT NULL,
                    "RadiusMeters" double precision NOT NULL DEFAULT 150,
                    "AlertOnArrival" boolean NOT NULL DEFAULT true,
                    "AlertOnDeparture" boolean NOT NULL DEFAULT true,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );
                CREATE INDEX IF NOT EXISTS "IX_SavedPlaces_FamilyId_IsActive"
                    ON familyos."SavedPlaces" ("FamilyId", "IsActive");
                CREATE TABLE IF NOT EXISTS familyos."MemberPlacePresences" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "FamilyId" uuid NOT NULL,
                    "PlaceId" uuid NOT NULL,
                    "MemberId" uuid NOT NULL,
                    "IsInside" boolean NOT NULL,
                    "LastLocationUpdatedAtUtc" timestamp with time zone NOT NULL,
                    "LastTransitionAtUtc" timestamp with time zone NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MemberPlacePresences_MemberId_PlaceId"
                    ON familyos."MemberPlacePresences" ("MemberId", "PlaceId");
                CREATE INDEX IF NOT EXISTS "IX_MemberPlacePresences_FamilyId_PlaceId"
                    ON familyos."MemberPlacePresences" ("FamilyId", "PlaceId");
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
