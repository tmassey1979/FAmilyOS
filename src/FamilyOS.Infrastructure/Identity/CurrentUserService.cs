using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Family;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    private readonly FamilyOsDbContext _db;
    private readonly ILogger<CurrentUserService> _logger;
    private bool _loaded;
    private Guid? _userId;
    private Guid? _familyId;
    private Guid? _memberId;
    private FamilyRole? _role;

    public CurrentUserService(IHttpContextAccessor http, FamilyOsDbContext db, ILogger<CurrentUserService> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
    }

    public string? ExternalIdentityId =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _http.HttpContext?.User?.FindFirstValue("sub");

    public Guid? UserId => _userId;
    public Guid? FamilyId => _familyId;
    public Guid? MemberId => _memberId;
    public FamilyRole? Role => _role;
    public bool IsAuthenticated => !string.IsNullOrEmpty(ExternalIdentityId) && _userId.HasValue;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loaded) return;
        _loaded = true;

        var total = Stopwatch.StartNew();
        var externalId = ExternalIdentityId;
        if (string.IsNullOrEmpty(externalId))
        {
            _logger.LogWarning("Current user load skipped: authenticated request has no external identity claim.");
            return;
        }

        var lookup = Stopwatch.StartNew();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId, ct);
        _logger.LogInformation("Current user database lookup completed in {ElapsedMs}ms. Found={Found}",
            lookup.Elapsed.TotalMilliseconds, user is not null);

        if (user is null)
        {
            var principal = _http.HttpContext?.User;
            var email = principal?.FindFirstValue(ClaimTypes.Email)
                ?? principal?.FindFirstValue("email")
                ?? $"{externalId}@familyos.local";
            var given = principal?.FindFirstValue(ClaimTypes.GivenName)
                ?? principal?.FindFirstValue("given_name")
                ?? principal?.FindFirstValue("preferred_username")
                ?? "Member";
            var familyName = principal?.FindFirstValue(ClaimTypes.Surname)
                ?? principal?.FindFirstValue("family_name")
                ?? "";

            user = User.Create(externalId, email, given, familyName);
            _db.Users.Add(user);
            try
            {
                var save = Stopwatch.StartNew();
                _logger.LogInformation("Current user insert started.");
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Current user insert completed in {ElapsedMs}ms.", save.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Current user insert failed; checking whether another request created the user.");
                var retry = Stopwatch.StartNew();
                user = await _db.Users.FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId, ct);
                _logger.LogInformation("Current user recovery lookup completed in {ElapsedMs}ms. Found={Found}",
                    retry.Elapsed.TotalMilliseconds, user is not null);
                if (user is null) return;
            }
        }

        _userId = user.Id;
        _familyId = user.FamilyId;
        _memberId = user.FamilyMemberId;

        if (user.FamilyMemberId.HasValue)
        {
            var memberLookup = Stopwatch.StartNew();
            var member = await _db.FamilyMembers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == user.FamilyMemberId, ct);
            _role = member?.Role;
            _logger.LogInformation("Current family-member lookup completed in {ElapsedMs}ms. Found={Found}",
                memberLookup.Elapsed.TotalMilliseconds, member is not null);
        }

        _logger.LogInformation("Current user context fully loaded in {ElapsedMs}ms. HasFamily={HasFamily}",
            total.Elapsed.TotalMilliseconds, _familyId.HasValue);
    }

    public async Task<(Guid FamilyId, Guid MemberId, FamilyRole Role)> RequireFamilyAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        if (!IsAuthenticated || !FamilyId.HasValue || !MemberId.HasValue)
            throw new FamilyOS.Application.Common.ForbiddenException("Not a family member.");
        return (FamilyId.Value, MemberId.Value, Role ?? FamilyRole.Child);
    }
}
