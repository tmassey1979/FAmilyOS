using FamilyOS.Application.Family;
using FamilyOS.Domain.Common;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FamilyOS.Infrastructure.Persistence;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/family")]
public class FamilyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly FamilyOsDbContext _db;

    public FamilyController(IMediator mediator, FamilyOsDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    /// <summary>Onboarding: create a new household. Requires auth but not existing family membership.</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<FamilyDto>> Create([FromBody] CreateFamilyBody body, CancellationToken ct)
    {
        var dto = await _mediator.Send(new CreateFamilyCommand(
            body.Name,
            body.TimeZone,
            body.Currency,
            body.OwnerDisplayName ?? "Owner",
            body.OwnerEmail ?? "owner@familyos.local",
            body.OwnerFirstName,
            body.OwnerLastName), ct);
        return Ok(dto);
    }

    [HttpGet]
    [Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
    public async Task<ActionResult<FamilyDto>> Get(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyFamilyQuery(), ct));

    [HttpGet("members")]
    [Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
    public async Task<ActionResult<List<MemberDto>>> Members(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMembersQuery(), ct));

    [HttpPost("members")]
    [Authorize(Policy = FamilyAuthPolicies.AdultOrOwner)]
    public async Task<ActionResult<MemberDto>> AddMember([FromBody] AddMemberBody body, CancellationToken ct)
    {
        var dto = await _mediator.Send(new AddFamilyMemberCommand(
            body.Email,
            body.DisplayName,
            body.Role,
            body.ExternalIdentityId,
            body.FirstName,
            body.LastName), ct);
        return Ok(dto);
    }


    [HttpPost("members/{id:guid}/change-role")]
    [Authorize(Policy = FamilyAuthPolicies.OwnerOnly)]
    public async Task<ActionResult<MemberDto>> ChangeRole(Guid id, [FromBody] ChangeRoleBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new ChangeMemberRoleCommand(id, body.Role), ct));

    [HttpPost("members/{id:guid}/deactivate")]
    [Authorize(Policy = FamilyAuthPolicies.OwnerOnly)]
    public async Task<ActionResult<MemberDto>> Deactivate(Guid id, CancellationToken ct)
    {
        var member = await _mediator.Send(new DeactivateMemberCommand(id), ct);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        // Serialize cleanup with any in-flight viewer grant operation for this member.
        var location = await _db.MemberLocations
            .FromSqlInterpolated($"""
                SELECT * FROM familyos."MemberLocations"
                WHERE "MemberId" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);
        var affectedSharers = await _db.MemberLocationViewerGrants.AsNoTracking()
            .Where(x => x.ViewerMemberId == id)
            .Select(x => x.SharingMemberId)
            .Distinct()
            .ToListAsync(ct);
        await _db.MemberLocationViewerGrants
            .Where(x => x.SharingMemberId == id || x.ViewerMemberId == id)
            .ExecuteDeleteAsync(ct);
        foreach (var sharerId in affectedSharers)
        {
            var stillHasViewer = await _db.MemberLocationViewerGrants.AnyAsync(
                x => x.SharingMemberId == sharerId, ct);
            if (stillHasViewer) continue;
            var sharerLocation = await _db.MemberLocations.SingleOrDefaultAsync(
                x => x.MemberId == sharerId, ct);
            if (sharerLocation is null) continue;
            sharerLocation.BatterySharingEnabled = false;
            sharerLocation.BatteryPercent = null;
            sharerLocation.IsCharging = null;
            sharerLocation.BatteryUpdatedAtUtc = null;
        }
        await _db.MemberLocationHistory
            .Where(x => x.MemberId == id)
            .ExecuteDeleteAsync(ct);
        await _db.MemberPlacePresences
            .Where(x => x.MemberId == id)
            .ExecuteDeleteAsync(ct);
        if (location is not null)
            _db.MemberLocations.Remove(location);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(member);
    }

    [HttpGet("decline-reasons")]
    [Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
    public async Task<ActionResult<List<DeclineReasonDto>>> DeclineReasons(CancellationToken ct)
        => Ok(await _mediator.Send(new GetDeclineReasonsQuery(), ct));

    public record CreateFamilyBody(
        string Name,
        string? TimeZone,
        string? Currency,
        string? OwnerDisplayName,
        string? OwnerEmail,
        string? OwnerFirstName,
        string? OwnerLastName);

    public record AddMemberBody(
        string Email,
        string DisplayName,
        FamilyRole Role,
        string? ExternalIdentityId,
        string? FirstName,
        string? LastName);

    public record ChangeRoleBody(FamilyRole Role);
}
