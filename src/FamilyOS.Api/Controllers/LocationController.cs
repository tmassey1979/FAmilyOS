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
            // Revocation immediately removes the last known coordinates.
            row.Latitude = null;
            row.Longitude = null;
            row.AccuracyMeters = null;
            row.LocationUpdatedAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { sharingEnabled = row.SharingEnabled, consentUpdatedAtUtc = row.ConsentUpdatedAtUtc });
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
        var row = await _db.Set<MemberLocation>().SingleOrDefaultAsync(x => x.MemberId == memberId, ct);
        if (row is null || !row.SharingEnabled)
            return Conflict(new { error = "Enable location sharing before sending a location." });

        row.FamilyId = familyId;
        row.Latitude = body.Latitude;
        row.Longitude = body.Longitude;
        row.AccuracyMeters = body.AccuracyMeters;
        row.LocationUpdatedAtUtc = measuredAt;
        await _db.SaveChangesAsync(ct);
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

    public sealed record ConsentBody(bool Enabled);
    public sealed record LocationBody(double Latitude, double Longitude, double AccuracyMeters, DateTime MeasuredAtUtc);
}
