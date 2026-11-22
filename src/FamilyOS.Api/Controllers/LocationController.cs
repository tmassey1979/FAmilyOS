using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Location;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Notifications;
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
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var row = await _db.MemberLocations
            .FromSqlInterpolated($"""
                SELECT * FROM familyos."MemberLocations"
                WHERE "FamilyId" = {familyId} AND "MemberId" = {memberId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);
        if (row is null)
        {
            row = new MemberLocation { FamilyId = familyId, MemberId = memberId };
            _db.MemberLocations.Add(row);
        }

        row.SharingEnabled = body.Enabled;
        row.ConsentUpdatedAtUtc = DateTime.UtcNow;
        if (!body.Enabled)
        {
            // The row lock serializes revocation with viewer-grant changes.
            await _db.MemberLocationViewerGrants
                .Where(x => x.FamilyId == familyId && x.SharingMemberId == memberId)
                .ExecuteDeleteAsync(ct);
            row.Latitude = null;
            row.Longitude = null;
            row.AccuracyMeters = null;
            row.LocationUpdatedAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
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

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var location = await _db.MemberLocations
            .FromSqlInterpolated($"""
                SELECT * FROM familyos."MemberLocations"
                WHERE "FamilyId" = {familyId} AND "MemberId" = {memberId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        if (!await IsActiveMemberAsync(familyId, viewerMemberId, ct))
            return NotFound(new { error = "That viewer is not an active member of your household." });
        if (location?.SharingEnabled != true)
            return Conflict(new { error = "Enable household location sharing before managing viewer access." });

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
        await transaction.CommitAsync(ct);
        return NoContent();
    }


    [HttpGet("places")]
    public async Task<IActionResult> GetPlaces(CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var places = await _db.SavedPlaces.AsNoTracking()
            .Where(x => x.FamilyId == familyId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new PlaceDto(x.Id, x.Name, x.Latitude, x.Longitude, x.RadiusMeters,
                x.AlertOnArrival, x.AlertOnDeparture, x.CreatedByMemberId == memberId))
            .ToListAsync(ct);
        return Ok(places);
    }

    [HttpPost("places")]
    public async Task<IActionResult> CreatePlace([FromBody] PlaceBody body, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var validation = ValidatePlace(body);
        if (validation is not null) return BadRequest(new { error = validation });
        var place = new SavedPlace
        {
            FamilyId = familyId,
            CreatedByMemberId = memberId,
            Name = body.Name.Trim(),
            Latitude = body.Latitude,
            Longitude = body.Longitude,
            RadiusMeters = body.RadiusMeters,
            AlertOnArrival = body.AlertOnArrival,
            AlertOnDeparture = body.AlertOnDeparture
        };
        _db.SavedPlaces.Add(place);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetPlaces), new { }, new PlaceDto(place.Id, place.Name,
            place.Latitude, place.Longitude, place.RadiusMeters, place.AlertOnArrival,
            place.AlertOnDeparture, true));
    }

    [HttpPut("places/{placeId:guid}")]
    public async Task<IActionResult> UpdatePlace(Guid placeId, [FromBody] PlaceBody body, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var validation = ValidatePlace(body);
        if (validation is not null) return BadRequest(new { error = validation });
        var place = await _db.SavedPlaces.SingleOrDefaultAsync(x =>
            x.Id == placeId && x.FamilyId == familyId && x.IsActive && !x.IsDeleted, ct);
        if (place is null) return NotFound();
        if (place.CreatedByMemberId != memberId) return Forbid();
        place.Name = body.Name.Trim();
        place.Latitude = body.Latitude;
        place.Longitude = body.Longitude;
        place.RadiusMeters = body.RadiusMeters;
        place.AlertOnArrival = body.AlertOnArrival;
        place.AlertOnDeparture = body.AlertOnDeparture;
        place.MarkUpdated();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("places/{placeId:guid}")]
    public async Task<IActionResult> DeletePlace(Guid placeId, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var place = await _db.SavedPlaces.SingleOrDefaultAsync(x =>
            x.Id == placeId && x.FamilyId == familyId && x.IsActive && !x.IsDeleted, ct);
        if (place is null) return NotFound();
        if (place.CreatedByMemberId != memberId) return Forbid();
        place.IsActive = false;
        place.SoftDelete();
        await _db.MemberPlacePresences
            .Where(x => x.FamilyId == familyId && x.PlaceId == placeId)
            .ExecuteDeleteAsync(ct);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? ValidatePlace(PlaceBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Name) || body.Name.Trim().Length > 120)
            return "Place name must contain 1 to 120 characters.";
        if (!double.IsFinite(body.Latitude) || body.Latitude is < -90 or > 90
            || !double.IsFinite(body.Longitude) || body.Longitude is < -180 or > 180)
            return "Place coordinates are outside valid bounds.";
        if (!double.IsFinite(body.RadiusMeters) || body.RadiusMeters is < 50 or > 5000)
            return "Geofence radius must be between 50 and 5,000 meters.";
        return null;
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

        await using (var transaction = await _db.Database.BeginTransactionAsync(ct))
        {
            // Serialize updates from this device so stale fixes cannot reverse geofence state.
            var location = await _db.MemberLocations
                .FromSqlInterpolated($"""
                    SELECT * FROM familyos."MemberLocations"
                    WHERE "FamilyId" = {familyId} AND "MemberId" = {memberId}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(ct);
            if (location?.SharingEnabled != true)
                return Conflict(new { error = "Enable location sharing before sending a location." });
            if (location.LocationUpdatedAtUtc.HasValue && measuredAt <= location.LocationUpdatedAtUtc.Value)
                return NoContent();

            location.Latitude = body.Latitude;
            location.Longitude = body.Longitude;
            location.AccuracyMeters = body.AccuracyMeters;
            location.LocationUpdatedAtUtc = measuredAt;

            var places = await _db.SavedPlaces
                .Where(x => x.FamilyId == familyId && x.IsActive && !x.IsDeleted)
                .ToListAsync(ct);
            var member = await _db.FamilyMembers.AsNoTracking()
                .SingleAsync(x => x.Id == memberId && x.FamilyId == familyId && x.IsActive, ct);

            foreach (var place in places)
            {
                var distance = GeofenceMath.DistanceMeters(body.Latitude, body.Longitude, place.Latitude, place.Longitude);
                var presence = await _db.MemberPlacePresences.SingleOrDefaultAsync(x =>
                    x.FamilyId == familyId && x.PlaceId == place.Id && x.MemberId == memberId && !x.IsDeleted, ct);
                if (presence is null)
                {
                    // First fix establishes a baseline; it must not create a misleading arrival alert.
                    _db.MemberPlacePresences.Add(new MemberPlacePresence
                    {
                        FamilyId = familyId,
                        PlaceId = place.Id,
                        MemberId = memberId,
                        IsInside = GeofenceMath.IsInside(false, distance, place.RadiusMeters),
                        LastLocationUpdatedAtUtc = measuredAt
                    });
                    continue;
                }

                if (measuredAt <= presence.LastLocationUpdatedAtUtc)
                    continue;

                // A 25m exit buffer prevents GPS drift at the boundary from spamming alerts.
                var isInside = GeofenceMath.IsInside(presence.IsInside, distance, place.RadiusMeters);
                var changed = isInside != presence.IsInside;
                presence.LastLocationUpdatedAtUtc = measuredAt;
                if (!changed)
                    continue;

                presence.IsInside = isInside;
                presence.LastTransitionAtUtc = measuredAt;
                var shouldAlert = isInside ? place.AlertOnArrival : place.AlertOnDeparture;
                if (!shouldAlert)
                    continue;

                var recipients = await (
                    from grant in _db.MemberLocationViewerGrants.AsNoTracking()
                    join viewer in _db.FamilyMembers.AsNoTracking() on grant.ViewerMemberId equals viewer.Id
                    where grant.FamilyId == familyId && grant.SharingMemberId == memberId
                        && viewer.FamilyId == familyId && viewer.IsActive && viewer.Id != memberId
                    select viewer.Id).Distinct().ToArrayAsync(ct);
                if (recipients.Length == 0)
                    continue;

                var title = isInside ? $"Arrived at {place.Name}" : $"Left {place.Name}";
                var bodyText = isInside
                    ? $"{member.DisplayName} arrived at {place.Name}."
                    : $"{member.DisplayName} left {place.Name}.";
                var notificationType = isInside ? NotificationType.LocationArrived : NotificationType.LocationDeparted;
                foreach (var recipientId in recipients)
                    _db.Notifications.Add(Notification.Create(familyId, recipientId, notificationType,
                        title, bodyText, place.Id, "SavedPlace"));
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

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

    public sealed record PlaceBody(string Name, double Latitude, double Longitude, double RadiusMeters, bool AlertOnArrival, bool AlertOnDeparture);\n    public sealed record PlaceDto(Guid Id, string Name, double Latitude, double Longitude, double RadiusMeters, bool AlertOnArrival, bool AlertOnDeparture, bool CanEdit);\n    public sealed record ConsentBody(bool Enabled);
    public sealed record ViewerGrantBody(bool Enabled);
    public sealed record LocationBody(double Latitude, double Longitude, double AccuracyMeters, DateTimeOffset MeasuredAtUtc);
}
