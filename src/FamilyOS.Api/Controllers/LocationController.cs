using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Location;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/location")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class LocationController : ControllerBase
{
    private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(24);
    private readonly FamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public LocationController(FamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("consent")]
    public async Task<IActionResult> GetConsent(CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var row = await _db.Set<MemberLocation>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.MemberId == memberId, ct);
        return Ok(new { sharingEnabled = row?.SharingEnabled ?? false, consentUpdatedAtUtc = row?.ConsentUpdatedAtUtc });
    }

    [HttpPut("consent")]
    public async Task<IActionResult> SetConsent([FromBody] ConsentBody body, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var row = await _db.Set<MemberLocation>().SingleOrDefaultAsync(x => x.MemberId == memberId, ct);
        if (row is null)
        {
            row = new MemberLocation { FamilyId = familyId, MemberId = memberId };
            _db.Add(row);
        }

        row.SharingEnabled = body.Enabled;
        row.ConsentUpdatedAtUtc = DateTime.UtcNow;
        if (!body.Enabled)
        {
            // Revocation immediately removes the last known coordinates and every viewer grant.
            await _db.MemberLocationViewerGrants.Where(x => x.FamilyId == familyId && x.SharingMemberId == memberId).ExecuteDeleteAsync(ct);
            row.Latitude = null;
            row.Longitude = null;
            row.AccuracyMeters = null;
            row.LocationUpdatedAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { sharingEnabled = row.SharingEnabled, consentUpdatedAtUtc = row.ConsentUpdatedAtUtc });
    }

    [HttpGet("viewers")]
    public async Task<IActionResult> GetViewerGrants(CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var granted = await _db.MemberLocationViewerGrants.AsNoTracking()
            .Where(x => x.FamilyId == familyId && x.SharingMemberId == memberId)
            .Select(x => x.ViewerMemberId).ToListAsync(ct);
        var members = await _db.FamilyMembers.AsNoTracking()
            .Where(x => x.FamilyId == familyId && x.IsActive && x.Id != memberId)
            .OrderBy(x => x.DisplayName)
            .Select(x => new { memberId = x.Id, displayName = x.DisplayName, canView = granted.Contains(x.Id) })
            .ToListAsync(ct);
        return Ok(members);
    }

    [HttpPut("viewers/{viewerMemberId:guid}")]
    public async Task<IActionResult> SetViewerGrant(Guid viewerMemberId, [FromBody] ViewerGrantBody body, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        if (viewerMemberId == memberId) return BadRequest(new { error = "You cannot grant location access to yourself." });
        if (!await IsActiveMemberAsync(familyId, viewerMemberId, ct))
            return NotFound(new { error = "That viewer is not an active member of your household." });

        if (body.Enabled)
        {
            // Idempotent under rapid taps or concurrent requests; the unique index is the final guard.
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO familyos."MemberLocationViewerGrants"
                    ("Id", "FamilyId", "SharingMemberId", "ViewerMemberId", "GrantedAtUtc")
                VALUES ({Guid.NewGuid()}, {familyId}, {memberId}, {viewerMemberId}, {DateTime.UtcNow})
                ON CONFLICT ("SharingMemberId", "ViewerMemberId") DO NOTHING
                """, ct);
        }
        else
        {
            await _db.MemberLocationViewerGrants
                .Where(x => x.FamilyId == familyId && x.SharingMemberId == memberId && x.ViewerMemberId == viewerMemberId)
                .ExecuteDeleteAsync(ct);
        }
        return NoContent();
    }

    [HttpPost("current")]
    public async Task<IActionResult> UpdateCurrentLocation([FromBody] LocationBody body, CancellationToken ct)
    {
        if (!double.IsFinite(body.Latitude) || body.Latitude is < -90 or > 90
            || !double.IsFinite(body.Longitude) || body.Longitude is < -180 or > 180
            || !double.IsFinite(body.AccuracyMeters) || body.AccuracyMeters is < 0 or > 10000)
            return BadRequest(new { error = "Coordinates or accuracy are outside valid bounds." });

        var now = DateTime.UtcNow;
        var measuredAt = body.MeasuredAtUtc.UtcDateTime;
        if (measuredAt > now.AddMinutes(5) || measuredAt < now.Subtract(MaximumAge))
            return BadRequest(new { error = "Location timestamp must be within the last 24 hours and not in the future." });

        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var updated = await _db.MemberLocations
            .Where(x => x.MemberId == memberId
                && x.FamilyId == familyId
                && x.SharingEnabled
                && _db.FamilyMembers.Any(m => m.Id == memberId && m.FamilyId == familyId && m.IsActive))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Latitude, (double?)body.Latitude)
                .SetProperty(x => x.Longitude, (double?)body.Longitude)
                .SetProperty(x => x.AccuracyMeters, (double?)body.AccuracyMeters)
                .SetProperty(x => x.LocationUpdatedAtUtc, (DateTime?)measuredAt), ct);

        if (updated == 0)
            return Conflict(new { error = "Enable location sharing before sending a location." });

        return NoContent();
    }

    [HttpGet("family")]
    public async Task<IActionResult> GetFamilyLocations(CancellationToken ct)
    {
        var (familyId, viewerMemberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, viewerMemberId, ct)) return Forbid();
        var cutoff = DateTime.UtcNow.Subtract(MaximumAge);
        var locations = await (
            from location in _db.Set<MemberLocation>().AsNoTracking()
            join member in _db.FamilyMembers.AsNoTracking() on location.MemberId equals member.Id
            where location.FamilyId == familyId
                && member.FamilyId == familyId
                && member.IsActive
                && location.SharingEnabled
                && _db.MemberLocationViewerGrants.Any(grant => grant.FamilyId == familyId
                    && grant.SharingMemberId == member.Id && grant.ViewerMemberId == viewerMemberId)
                && location.LocationUpdatedAtUtc != null
                && location.LocationUpdatedAtUtc >= cutoff
            orderby location.LocationUpdatedAtUtc descending
            select new
            {
                memberId = member.Id,
                displayName = member.DisplayName,
                latitude = location.Latitude,
                longitude = location.Longitude,
                accuracyMeters = location.AccuracyMeters,
                updatedAtUtc = location.LocationUpdatedAtUtc
            }).ToListAsync(ct);

        return Ok(locations);
    }

    private Task<bool> IsActiveMemberAsync(Guid familyId, Guid memberId, CancellationToken ct)
        => _db.FamilyMembers.AnyAsync(x => x.Id == memberId && x.FamilyId == familyId && x.IsActive, ct);

    public sealed record ConsentBody(bool Enabled);
    public sealed record ViewerGrantBody(bool Enabled);
    public sealed record LocationBody(double Latitude, double Longitude, double AccuracyMeters, DateTimeOffset MeasuredAtUtc);
}
