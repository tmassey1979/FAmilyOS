using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Meals;
using FamilyOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/meals/shopping-preview")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class MealShoppingPreviewController : ControllerBase
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public MealShoppingPreviewController(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<ActionResult<MealShoppingPreviewDto>> Preview(
        [FromQuery] DateTime? weekStart, [FromQuery] DateTime? day, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || _current.FamilyId is null) return Unauthorized();
        var week = MealPlan.ToMonday(day ?? weekStart ?? DateTime.UtcNow);
        var plan = await _db.MealPlans.AsNoTracking().Include(x => x.Entries)
            .FirstOrDefaultAsync(x => x.FamilyId == _current.FamilyId && x.WeekStartUtc == week, ct);
        if (plan is null) return Ok(new MealShoppingPreviewDto(day is null ? "Week" : "Day", new List<string>()));

        var entries = plan.Entries.Where(x => !x.IsDeleted && (day is null || x.DayUtc.Date == day.Value.Date)).ToList();
        var names = entries.Where(x => x.RecipeId is null).SelectMany(x => x.IngredientList()).ToList();
        var recipeIds = entries.Where(x => x.RecipeId.HasValue).Select(x => x.RecipeId!.Value).Distinct().ToArray();
        if (recipeIds.Length > 0)
        {
            var rows = await (from row in _db.RecipeIngredients.AsNoTracking()
                              join ingredient in _db.IngredientCatalog.AsNoTracking() on row.IngredientId equals ingredient.Id
                              where recipeIds.Contains(row.RecipeId)
                              select new { row.RecipeId, row.IngredientId, ingredient.Name, row.Quantity, row.Unit })
                .ToListAsync(ct);
            var structuredIds = rows.Select(x => x.RecipeId).ToHashSet();
            var legacyIds = recipeIds.Where(x => !structuredIds.Contains(x)).ToArray();
            if (legacyIds.Length > 0)
            {
                var legacy = await _db.Recipes.AsNoTracking().Where(x => legacyIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.IngredientsCsv }).ToListAsync(ct);
                var legacyById = legacy.ToDictionary(x => x.Id, x => x.IngredientsCsv);
                foreach (var entry in entries.Where(x => x.RecipeId.HasValue && legacyById.ContainsKey(x.RecipeId.Value)))
                    if (!string.IsNullOrWhiteSpace(legacyById[entry.RecipeId!.Value]))
                        names.AddRange(legacyById[entry.RecipeId.Value]!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            var uses = entries.Where(x => x.RecipeId.HasValue).GroupBy(x => x.RecipeId!.Value)
                .ToDictionary(x => x.Key, x => x.Count());
            var aggregate = new Dictionary<(Guid IngredientId, string Unit), (string Name, decimal? Quantity, string Unit)>();
            foreach (var row in rows)
            {
                var unit = NormalizeUnit(row.Unit);
                var quantity = row.Quantity is null ? null : ToBaseQuantity(row.Quantity.Value, row.Unit) * uses[row.RecipeId];
                var key = (row.IngredientId, unit);
                if (aggregate.TryGetValue(key, out var current))
                    aggregate[key] = (current.Name, current.Quantity is null || quantity is null
                        ? null : current.Quantity.Value + quantity.Value, current.Unit);
                else aggregate[key] = (row.Name, quantity, unit);
            }
            foreach (var item in aggregate.Values)
                names.Add(item.Quantity is null ? item.Name : FormatLine(item.Quantity.Value, item.Unit, item.Name));
        }

        return Ok(new MealShoppingPreviewDto(day is null ? "Week" : "Day",
            names.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x).ToList()));
    }

    private static string NormalizeUnit(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return raw.Trim().ToLowerInvariant() switch
        {
            "tsp" or "teaspoon" or "teaspoons" or "tbsp" or "tablespoon" or "tablespoons" or "cup" or "cups" or "fl oz" or "fluid ounce" or "fluid ounces" or "ml" or "milliliter" or "milliliters" or "l" or "liter" or "liters" => "tsp",
            "oz" or "ounce" or "ounces" or "lb" or "lbs" or "pound" or "pounds" or "g" or "gram" or "grams" or "kg" or "kilogram" or "kilograms" => "g",
            "each" or "ea" or "item" or "items" => "each",
            var other => other
        };
    }

    private static decimal ToBaseQuantity(decimal quantity, string? raw)
        => raw?.Trim().ToLowerInvariant() switch
        {
            "tbsp" or "tablespoon" or "tablespoons" => quantity * 3m,
            "cup" or "cups" => quantity * 48m,
            "fl oz" or "fluid ounce" or "fluid ounces" => quantity * 6m,
            "ml" or "milliliter" or "milliliters" => quantity * 0.202884m,
            "l" or "liter" or "liters" => quantity * 202.884m,
            "oz" or "ounce" or "ounces" => quantity * 28.3495m,
            "lb" or "lbs" or "pound" or "pounds" => quantity * 453.592m,
            "kg" or "kilogram" or "kilograms" => quantity * 1000m,
            _ => quantity
        };

    private static string FormatLine(decimal quantity, string unit, string name)
    {
        string displayUnit;
        decimal displayQuantity;
        if (unit == "tsp")
        {
            if (quantity >= 48m) { displayUnit = "cup"; displayQuantity = quantity / 48m; }
            else if (quantity >= 3m) { displayUnit = "tbsp"; displayQuantity = quantity / 3m; }
            else { displayUnit = "tsp"; displayQuantity = quantity; }
        }
        else if (unit == "g")
        {
            if (quantity >= 453.592m) { displayUnit = "lb"; displayQuantity = quantity / 453.592m; }
            else if (quantity >= 28.3495m) { displayUnit = "oz"; displayQuantity = quantity / 28.3495m; }
            else { displayUnit = "g"; displayQuantity = quantity; }
        }
        else { displayUnit = unit; displayQuantity = quantity; }
        return string.Join(" ", new[] { displayQuantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), displayUnit, name }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}

public sealed record MealShoppingPreviewDto(string Scope, List<string> Items);
