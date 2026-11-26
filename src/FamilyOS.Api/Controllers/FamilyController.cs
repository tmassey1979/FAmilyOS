using FamilyOS.Application.Family;
using FamilyOS.Domain.Common;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Persistence;
using FamilyOS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/family")]
public class FamilyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly FamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public FamilyController(IMediator mediator, FamilyOsDbContext db, ICurrentUserService current)
    {
        _mediator = mediator;
        _db = db;
        _current = current;
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

    /// <summary>Owner-confirmed permanent removal of all household data; sign-in identities are retained.</summary>
    [HttpPost("reset")]
    [Authorize(Policy = FamilyAuthPolicies.OwnerOnly)]
    public async Task<IActionResult> Reset([FromBody] ResetFamilyBody body, CancellationToken ct)
    {
        if (!string.Equals(body.Confirmation, "RESET FAMILY", StringComparison.Ordinal))
            return BadRequest(new { detail = "Type RESET FAMILY exactly to confirm." });

        await _current.EnsureLoadedAsync(ct);
        if (!_current.FamilyId.HasValue || !_current.UserId.HasValue || _current.Role != FamilyRole.Owner)
            return Forbid();

        var familyId = _current.FamilyId.Value;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var users = await _db.Users.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ToListAsync(ct);
        foreach (var user in users) user.ClearFamilyAssociation();

        await _db.ChatMessages.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.TaskHistory.IgnoreQueryFilters().Where(x => _db.Tasks.IgnoreQueryFilters().Any(t => t.Id == x.TaskId && t.FamilyId == familyId)).ExecuteDeleteAsync(ct);
        await _db.TaskTimeSegments.IgnoreQueryFilters().Where(x => _db.Tasks.IgnoreQueryFilters().Any(t => t.Id == x.TaskId && t.FamilyId == familyId)).ExecuteDeleteAsync(ct);
        await _db.RequestHistory.IgnoreQueryFilters().Where(x => _db.Requests.IgnoreQueryFilters().Any(r => r.Id == x.RequestId && r.FamilyId == familyId)).ExecuteDeleteAsync(ct);
        await _db.RequestQuestions.IgnoreQueryFilters().Where(x => _db.Requests.IgnoreQueryFilters().Any(r => r.Id == x.RequestId && r.FamilyId == familyId)).ExecuteDeleteAsync(ct);
        await _db.ExecutionItems.IgnoreQueryFilters().Where(x => _db.ExecutionPlans.IgnoreQueryFilters().Any(p => p.Id == x.ExecutionPlanId && _db.Requests.IgnoreQueryFilters().Any(r => r.Id == p.RequestId && r.FamilyId == familyId))).ExecuteDeleteAsync(ct);

        await _db.MemberLocationViewerGrants.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.MemberPlacePresences.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.MemberLocationHistory.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.MemberLocations.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.CheckInRequests.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.SavedPlaces.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.FamilyInvites.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.DeclineReasons.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.TaskTimingRecords.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.RecurrenceDefinitions.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.CalendarEvents.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.GoogleCalendarConnections.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.ChatChannels.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.RequestTypeDefinitions.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.PolicyOverrides.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.ExecutionPlans.IgnoreQueryFilters().Where(x => _db.Requests.IgnoreQueryFilters().Any(r => r.Id == x.RequestId && r.FamilyId == familyId)).ExecuteDeleteAsync(ct);
        await _db.Conditions.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.ApprovalPolicies.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.ProcurementItems.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.Products.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.Carts.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.Notifications.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.DeviceRegistrations.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.MealEntries.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.MealPlans.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.Recipes.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.Tasks.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.Requests.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.FamilyMembers.IgnoreQueryFilters().Where(x => x.FamilyId == familyId).ExecuteDeleteAsync(ct);
        await _db.Families.IgnoreQueryFilters().Where(x => x.Id == familyId).ExecuteDeleteAsync(ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
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
        => Ok(await _mediator.Send(new DeactivateMemberCommand(id), ct));

    /// <summary>Self or Owner can update a member's display name.</summary>
    [HttpPut("members/{id:guid}/display-name")]
    [Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
    public async Task<ActionResult<MemberDto>> UpdateDisplayName(Guid id, [FromBody] DisplayNameBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.DisplayName))
            return BadRequest(new { detail = "Display name is required." });
        return Ok(await _mediator.Send(new UpdateMemberDisplayNameCommand(id, body.DisplayName.Trim()), ct));
    }

    [HttpGet("decline-reasons")]
    [Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
    public async Task<ActionResult<List<DeclineReasonDto>>> DeclineReasons(CancellationToken ct)
        => Ok(await _mediator.Send(new GetDeclineReasonsQuery(), ct));

    public record ResetFamilyBody(string Confirmation);

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

    public record DisplayNameBody(string DisplayName);
}
