using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Meals;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class RecipePreferencesController : ControllerBase
{
    private readonly FamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public RecipePreferencesController(FamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<List<RecipeMemberPreferenceDto>>> List(
        [FromQuery] string recipeIds, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var ids = (recipeIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().Take(48).ToArray();
        if (ids.Length == 0) return Ok(new List<RecipeMemberPreferenceDto>());

        var rows = await (from preference in _db.RecipeMemberPreferences.AsNoTracking()
                          join member in _db.FamilyMembers.AsNoTracking() on preference.MemberId equals member.Id
                          where preference.FamilyId == familyId && ids.Contains(preference.RecipeId)
                              && member.FamilyId == familyId && member.IsActive
                          orderby member.DisplayName
                          select new RecipeMemberPreferenceDto(
                              preference.RecipeId, preference.MemberId, member.DisplayName,
                              preference.Preference == null ? null : preference.Preference.ToString(),
                              preference.IsFavorite, preference.MemberId == memberId))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPut("{recipeId:guid}/preference")]
    public async Task<ActionResult<RecipeMemberPreferenceDto>> Set(
        Guid recipeId, [FromBody] SetRecipeMemberPreferenceBody body, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (body.Preference is not null
            && !Enum.TryParse<RecipePreferenceKind>(body.Preference, true, out _))
            return BadRequest("Preference must be Like, Dislike, or null.");

        var recipeExists = await _db.Recipes.AnyAsync(
            recipe => recipe.Id == recipeId
                && (recipe.FamilyId == familyId || recipe.FamilyId == Guid.Empty), ct);
        if (!recipeExists) return NotFound();

        var preferenceKind = body.Preference is null
            ? (RecipePreferenceKind?)null
            : Enum.Parse<RecipePreferenceKind>(body.Preference, true);
        var row = await _db.RecipeMemberPreferences.FirstOrDefaultAsync(
            item => item.FamilyId == familyId && item.RecipeId == recipeId && item.MemberId == memberId, ct);

        if (row is null)
        {
            if (preferenceKind is null && !body.IsFavorite)
                return Ok(new RecipeMemberPreferenceDto(recipeId, memberId, string.Empty, null, false, true));
            row = RecipeMemberPreference.Create(familyId, recipeId, memberId, preferenceKind, body.IsFavorite);
            _db.RecipeMemberPreferences.Add(row);
        }
        else if (preferenceKind is null && !body.IsFavorite)
        {
            row.SoftDelete();
        }
        else
        {
            row.Set(preferenceKind, body.IsFavorite);
        }

        await _db.SaveChangesAsync(ct);
        var displayName = await _db.FamilyMembers.AsNoTracking()
            .Where(member => member.Id == memberId && member.FamilyId == familyId)
            .Select(member => member.DisplayName).FirstAsync(ct);
        return Ok(new RecipeMemberPreferenceDto(recipeId, memberId, displayName,
            preferenceKind?.ToString(), body.IsFavorite, true));
    }
}

public sealed record SetRecipeMemberPreferenceBody(string? Preference, bool IsFavorite);
public sealed record RecipeMemberPreferenceDto(
    Guid RecipeId, Guid MemberId, string MemberName, string? Preference, bool IsFavorite, bool IsCurrentMember);
