using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsureAvatarSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE familyos."FamilyMembers" ADD COLUMN IF NOT EXISTS "AvatarUrl" character varying(512) NULL;""",
                ct);
            logger.LogInformation("Ensured FamilyMembers.AvatarUrl column.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure FamilyMembers.AvatarUrl column.");
        }
    }
}
