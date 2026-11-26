using FamilyOS.Application.Interfaces;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/ingredients")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class IngredientCatalogController : ControllerBase
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public IngredientCatalogController(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<IngredientCatalogDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] int limit = 25,
        CancellationToken ct = default)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || _current.FamilyId is null)
            return Unauthorized();

        limit = Math.Clamp(limit, 1, 50);
        var query = _db.IngredientCatalog.AsNoTracking()
            .Where(x => x.FamilyId == null || x.FamilyId == _current.FamilyId);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.SearchKey.Contains(term)
                || x.Name.ToLower().Contains(term)
                || (x.AliasesCsv != null && x.AliasesCsv.ToLower().Contains(term)));
        }

        var results = await query
            .OrderByDescending(x => x.IsBuiltIn)
            .ThenBy(x => x.Name)
            .Take(limit)
            .Select(x => new IngredientCatalogDto(
                x.Id, x.Name, x.Category, x.AliasesCsv, x.IsBuiltIn))
            .ToListAsync(ct);

        return Ok(results);
    }
}

public sealed record IngredientCatalogDto(
    Guid Id, string Name, string Category, string? AliasesCsv, bool IsBuiltIn);
