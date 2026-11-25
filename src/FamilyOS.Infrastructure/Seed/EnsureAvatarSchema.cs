using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Seed;

public static class EnsureAvatarSchema
{
    public static async Task RunAsync(FamilyOsDbContext db, ILogger logger)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE familyos.\"FamilyMembers\"
                    ADD COLUMN IF NOT EXISTS \"AvatarUrl\" character varying(500) NULL;
                """);
            logger.LogInformation("AvatarUrl column on FamilyMembers ensured");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ensure AvatarUrl schema failed");
        }
    }
}
