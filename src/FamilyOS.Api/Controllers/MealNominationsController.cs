using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Meals;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/meals/nominations")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class MealNominationsController : ControllerBase
{
    private readonly FamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public MealNominationsController(FamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MealNominationDto>>> GetWeek([FromQuery] DateTime? weekStart, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var week = MealPlan.ToMonday(weekStart ?? DateTime.UtcNow);
        var rows = await _db.MealNominations.AsNoTracking()
            .Where(x => x.FamilyId == familyId && x.WeekStartUtc == week)
            .OrderByDescending(x => x.Status == MealNominationStatus.Open)
            .ThenBy(x => x.Title)
            .Select(x => new
            {
                Nomination = x,
                VoteCount = _db.MealNominationVotes.Count(v => v.NominationId == x.Id && v.FamilyId == familyId),
                HasVoted = _db.MealNominationVotes.Any(v => v.NominationId == x.Id && v.MemberId == memberId)
            }).ToListAsync(ct);
        return Ok(rows.Select(x => new MealNominationDto(
            x.Nomination.Id, x.Nomination.Title, x.Nomination.RecipeId, x.Nomination.Note,
            x.Nomination.ProposedByMemberId, x.Nomination.Status.ToString(), x.VoteCount, x.HasVoted,
            x.Nomination.PlannedDayUtc, x.Nomination.PlannedSlot)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<MealNominationDto>> Nominate([FromBody] NominateMealBody body, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (body.RecipeId is Guid recipeId)
        {
            var valid = await _db.Recipes.AnyAsync(r => r.Id == recipeId
                && (r.FamilyId == familyId || r.FamilyId == Guid.Empty), ct);
            if (!valid) return BadRequest(new { error = "That recipe is not available to this family." });
        }

        var week = MealPlan.ToMonday(body.WeekStartUtc ?? DateTime.UtcNow);
        var duplicate = await _db.MealNominations.AnyAsync(x => x.FamilyId == familyId
            && x.WeekStartUtc == week && x.Status == MealNominationStatus.Open
            && x.Title.ToLower() == body.Title.Trim().ToLower(), ct);
        if (duplicate) return Conflict(new { error = "That meal has already been nominated for this week." });

        var nomination = MealNomination.Create(familyId, week, body.Title, memberId, body.RecipeId, body.Note);
        _db.MealNominations.Add(nomination);
        await _db.SaveChangesAsync(ct);
        return Ok(new MealNominationDto(nomination.Id, nomination.Title, nomination.RecipeId,
            nomination.Note, nomination.ProposedByMemberId, nomination.Status.ToString(), 0, false, null, null));
    }

    [HttpPut("{id:guid}/vote")]
    public async Task<IActionResult> Vote(Guid id, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var nomination = await _db.MealNominations.FirstOrDefaultAsync(x => x.Id == id
            && x.FamilyId == familyId && x.Status == MealNominationStatus.Open, ct);
        if (nomination is null) return NotFound(new { error = "This nomination is no longer open for voting." });
        var existing = await _db.MealNominationVotes.FirstOrDefaultAsync(x => x.NominationId == id && x.MemberId == memberId, ct);
        if (existing is null)
            _db.MealNominationVotes.Add(MealNominationVote.Create(familyId, id, memberId));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/vote")]
    public async Task<IActionResult> RemoveVote(Guid id, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var vote = await _db.MealNominationVotes.FirstOrDefaultAsync(x => x.NominationId == id
            && x.FamilyId == familyId && x.MemberId == memberId, ct);
        if (vote is not null)
        {
            vote.SoftDelete();
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<ActionResult<MealNominationDto>> Decide(Guid id, [FromBody] DecideMealNominationBody body, CancellationToken ct)
    {
        var (familyId, memberId, role) = await _current.RequireFamilyAsync(ct);
        if (role is not (FamilyRole.Owner or FamilyRole.Adult))
            return Forbid();

        var nomination = await _db.MealNominations.FirstOrDefaultAsync(x => x.Id == id
            && x.FamilyId == familyId && x.Status == MealNominationStatus.Open, ct);
        if (nomination is null) return NotFound(new { error = "This nomination is no longer open." });

        nomination.Decide(memberId, body.Selected, body.DayUtc, body.Slot);
        if (body.Selected)
        {
            var week = await _db.MealPlans.Include(p => p.Entries)
                .FirstOrDefaultAsync(p => p.FamilyId == familyId && p.WeekStartUtc == nomination.WeekStartUtc, ct);
            if (week is null)
            {
                week = MealPlan.Create(familyId, nomination.WeekStartUtc);
                _db.MealPlans.Add(week);
            }
            var slot = Enum.Parse<MealSlot>(body.Slot!, true);
            week.AddEntry(body.DayUtc!.Value, slot, nomination.Title,
                nomination.Note, recipeId: nomination.RecipeId);
        }
        await _db.SaveChangesAsync(ct);
        var count = await _db.MealNominationVotes.CountAsync(v => v.NominationId == id && v.FamilyId == familyId, ct);
        return Ok(new MealNominationDto(nomination.Id, nomination.Title, nomination.RecipeId,
            nomination.Note, nomination.ProposedByMemberId, nomination.Status.ToString(), count,
            false, nomination.PlannedDayUtc, nomination.PlannedSlot));
    }
}

public sealed record NominateMealBody(string Title, DateTime? WeekStartUtc = null, Guid? RecipeId = null, string? Note = null);
public sealed record DecideMealNominationBody(bool Selected, DateTime? DayUtc = null, string? Slot = null);
public sealed record MealNominationDto(Guid Id, string Title, Guid? RecipeId, string? Note,
    Guid ProposedByMemberId, string Status, int VoteCount, bool HasVoted, DateTime? PlannedDayUtc, string? PlannedSlot);
