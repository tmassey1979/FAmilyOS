using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.CheckIns;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Notifications;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/checkins")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class CheckInsController : ControllerBase
{
    private readonly FamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public CheckInsController(FamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> Mine([FromQuery] bool includeCompleted = true, CancellationToken ct = default)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();

        var query = from item in _db.CheckInRequests.AsNoTracking()
                    join requester in _db.FamilyMembers.AsNoTracking() on item.RequesterMemberId equals requester.Id
                    join target in _db.FamilyMembers.AsNoTracking() on item.TargetMemberId equals target.Id
                    where item.FamilyId == familyId && (item.RequesterMemberId == memberId || item.TargetMemberId == memberId)
                        && requester.IsActive && target.IsActive
                    orderby item.CreatedAtUtc descending
                    select new { item, requester.DisplayName, TargetName = target.DisplayName };

        var rows = await query.Take(100).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var result = rows.Select(x => new CheckInDto(
            x.item.Id, x.item.RequesterMemberId, x.DisplayName, x.item.TargetMemberId, x.TargetName,
            x.item.CreatedAtUtc, x.item.DueAtUtc, x.item.GetEffectiveStatus(now),
            x.item.RespondedAtUtc, x.item.ResponseMessage, x.item.TargetMemberId == memberId)).ToList();
        if (!includeCompleted)
            result = result.Where(x => x.Status is "Pending" or "Overdue").ToList();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCheckInBody body, CancellationToken ct)
    {
        var (familyId, requesterId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, requesterId, ct)) return Forbid();
        if (body.TargetMemberId == Guid.Empty || body.TargetMemberId == requesterId)
            return BadRequest(new { error = "Choose another active household member." });
        if (body.DueAtUtc <= DateTimeOffset.UtcNow || body.DueAtUtc > DateTimeOffset.UtcNow.AddDays(7))
            return BadRequest(new { error = "Choose a due time within the next seven days." });
        if (!await IsActiveMemberAsync(familyId, body.TargetMemberId, ct))
            return NotFound(new { error = "That person is not an active household member." });

        var target = await _db.FamilyMembers.AsNoTracking()
            .SingleAsync(x => x.Id == body.TargetMemberId && x.FamilyId == familyId, ct);
        var requester = await _db.FamilyMembers.AsNoTracking()
            .SingleAsync(x => x.Id == requesterId && x.FamilyId == familyId, ct);

        var item = CheckInRequest.Create(familyId, requesterId, body.TargetMemberId, body.DueAtUtc.UtcDateTime);
        _db.CheckInRequests.Add(item);
        _db.Notifications.Add(Notification.Create(familyId, body.TargetMemberId,
            NotificationType.CheckInRequested, "Check-in requested",
            $"{requester.DisplayName} asked you to check in by {item.DueAtUtc.ToLocalTime():g}. Respond when you can; FamilyOS does not collect your location for a check-in.",
            item.Id, "CheckInRequest"));
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Mine), new { }, new CheckInDto(item.Id, requesterId,
            requester.DisplayName, target.Id, target.DisplayName, item.CreatedAtUtc, item.DueAtUtc,
            item.Status, null, null, true));
    }

    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] RespondCheckInBody body, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var item = await _db.CheckInRequests.SingleOrDefaultAsync(x =>
            x.Id == id && x.FamilyId == familyId && x.TargetMemberId == memberId, ct);
        if (item is null) return NotFound();
        try { item.RespondSafe(body.Message); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }

        var member = await _db.FamilyMembers.AsNoTracking().SingleAsync(x => x.Id == memberId, ct);
        _db.Notifications.Add(Notification.Create(familyId, item.RequesterMemberId,
            NotificationType.CheckInResponded, "Check-in completed",
            $"{member.DisplayName} responded that they're safe.",
            item.Id, "CheckInRequest"));
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = item.Id, status = item.Status, respondedAtUtc = item.RespondedAtUtc });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (!await IsActiveMemberAsync(familyId, memberId, ct)) return Forbid();
        var item = await _db.CheckInRequests.SingleOrDefaultAsync(x =>
            x.Id == id && x.FamilyId == familyId && x.RequesterMemberId == memberId, ct);
        if (item is null) return NotFound();
        try { item.Cancel(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<bool> IsActiveMemberAsync(Guid familyId, Guid memberId, CancellationToken ct)
        => _db.FamilyMembers.AnyAsync(x => x.Id == memberId && x.FamilyId == familyId && x.IsActive, ct);

    public sealed record CreateCheckInBody(Guid TargetMemberId, DateTimeOffset DueAtUtc);
    public sealed record RespondCheckInBody(string? Message);
    public sealed record CheckInDto(Guid Id, Guid RequesterMemberId, string RequesterName,
        Guid TargetMemberId, string TargetName, DateTime CreatedAtUtc, DateTime DueAtUtc,
        string Status, DateTime? RespondedAtUtc, string? ResponseMessage, bool CanRespond);
}
