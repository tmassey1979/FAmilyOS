using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Meals;
using FamilyOS.Domain.Procurement;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Meals;

public record MealEntryDto(
    Guid Id, DateTime DayUtc, string Slot, string DishName, string? Notes, string? IngredientsCsv, Guid? RecipeId = null);

public record MealPlanDto(
    Guid Id, DateTime WeekStartUtc, string? Notes, List<MealEntryDto> Entries);

public record GetMealPlanWeekQuery(DateTime? WeekStartUtc = null) : IRequest<MealPlanDto>;

public record UpsertMealEntryCommand(
    DateTime DayUtc,
    string Slot,
    string DishName,
    string? Notes = null,
    string? IngredientsCsv = null,
    DateTime? WeekStartUtc = null,
    Guid? RecipeId = null
) : IRequest<MealEntryDto>;

public record RemoveMealEntryCommand(Guid EntryId) : IRequest;

public record PushMealIngredientsToQueueCommand(DateTime? WeekStartUtc = null, DateTime? DayUtc = null) : IRequest<int>;

public class MealCommandHandlers :
    IRequestHandler<GetMealPlanWeekQuery, MealPlanDto>,
    IRequestHandler<UpsertMealEntryCommand, MealEntryDto>,
    IRequestHandler<RemoveMealEntryCommand>,
    IRequestHandler<PushMealIngredientsToQueueCommand, int>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public MealCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<MealPlanDto> Handle(GetMealPlanWeekQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        var week = MealPlan.ToMonday(query.WeekStartUtc ?? DateTime.UtcNow);
        var plan = await LoadOrCreateAsync(week, ct);
        return ToDto(plan);
    }

    public async Task<MealEntryDto> Handle(UpsertMealEntryCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        if (!Enum.TryParse<MealSlot>(cmd.Slot, ignoreCase: true, out var slot))
            throw new ValidationException("Slot", "Slot must be Breakfast, Lunch, Dinner, or Snack.");

        var week = MealPlan.ToMonday(cmd.WeekStartUtc ?? cmd.DayUtc);
        var plan = await LoadOrCreateAsync(week, ct);
        var entry = plan.UpsertEntry(cmd.DayUtc, slot, cmd.DishName, cmd.Notes, cmd.IngredientsCsv, cmd.RecipeId);
        await _db.SaveChangesAsync(ct);
        return ToEntryDto(entry);
    }

    public async Task Handle(RemoveMealEntryCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        var entry = await _db.MealEntries
            .FirstOrDefaultAsync(e => e.Id == cmd.EntryId && e.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("MealEntry", cmd.EntryId);
        var plan = await _db.MealPlans
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.Id == entry.MealPlanId, ct)
            ?? throw new NotFoundException("MealPlan", entry.MealPlanId);
        plan.RemoveEntry(cmd.EntryId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(PushMealIngredientsToQueueCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuth();
        var week = MealPlan.ToMonday(cmd.WeekStartUtc ?? DateTime.UtcNow);
        var plan = await _db.MealPlans
            .AsNoTracking()
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.FamilyId == _current.FamilyId && p.WeekStartUtc == week, ct);
        if (plan is null) return 0;

        var plannedEntries = plan.Entries.Where(e => !e.IsDeleted
            && (cmd.DayUtc is null || e.DayUtc.Date == cmd.DayUtc.Value.Date)).ToList();
        var names = plannedEntries.Where(e => e.RecipeId is null).SelectMany(e => e.IngredientList()).ToList();

        var recipeIds = plannedEntries.Where(e => e.RecipeId.HasValue)
            .Select(e => e.RecipeId!.Value).Distinct().ToArray();
        if (recipeIds.Length > 0)
        {
            var recipeRows = await (from row in _db.RecipeIngredients.AsNoTracking()
                                    join ingredient in _db.IngredientCatalog.AsNoTracking()
                                        on row.IngredientId equals ingredient.Id
                                    where recipeIds.Contains(row.RecipeId)
                                    select new
                                    {
                                        row.RecipeId, row.IngredientId, ingredient.Name,
                                        row.Quantity, row.Unit
                                    }).ToListAsync(ct);

            var structuredRecipeIds = recipeRows.Select(x => x.RecipeId).ToHashSet();
            var legacyRecipeIds = recipeIds.Where(id => !structuredRecipeIds.Contains(id)).ToArray();
            if (legacyRecipeIds.Length > 0)
            {
                var legacyRecipes = await _db.Recipes.AsNoTracking()
                    .Where(x => legacyRecipeIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.IngredientsCsv }).ToListAsync(ct);
                var legacyById = legacyRecipes.ToDictionary(x => x.Id, x => x.IngredientsCsv);
                foreach (var entry in plannedEntries.Where(x => x.RecipeId.HasValue && legacyById.ContainsKey(x.RecipeId.Value)))
                {
                    var csv = legacyById[entry.RecipeId!.Value];
                    if (!string.IsNullOrWhiteSpace(csv))
                        names.AddRange(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
            }

            var recipeUseCounts = plannedEntries.Where(e => e.RecipeId.HasValue)
                .GroupBy(e => e.RecipeId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var aggregate = new Dictionary<(Guid IngredientId, string Unit), (string Name, decimal? Quantity, string Unit)>();
            foreach (var row in recipeRows)
            {
                var uses = recipeUseCounts[row.RecipeId];
                var unit = NormalizeShoppingUnit(row.Unit);
                var key = (row.IngredientId, unit);
                decimal? quantity = row.Quantity is null ? null : ToBaseShoppingQuantity(row.Quantity.Value, row.Unit) * uses;
                if (aggregate.TryGetValue(key, out var current))
                    aggregate[key] = (current.Name, current.Quantity is null || quantity is null
                        ? null : current.Quantity.Value + quantity.Value, current.Unit);
                else
                    aggregate[key] = (row.Name, quantity, unit);
            }

            foreach (var item in aggregate.Values)
                names.Add(item.Quantity is null ? item.Name : FormatShoppingLine(item.Quantity.Value, item.Unit, item.Name));
        }

        names = names.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return 0;

        var existing = await _db.ProcurementItems
            .Where(i => i.FamilyId == _current.FamilyId
                && (i.Status == ProcurementItemStatus.Queued || i.Status == ProcurementItemStatus.InCart))
            .Select(i => i.Name)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var approver = _current.MemberId!.Value;
        foreach (var name in names)
        {
            if (existingSet.Contains(name)) continue;
            _db.ProcurementItems.Add(ProcurementItem.Create(
                _current.FamilyId!.Value,
                name,
                approver,
                category: "Meal plan"));
            existingSet.Add(name);
            added++;
        }

        if (added > 0)
            await _db.SaveChangesAsync(ct);
        return added;
    }


    private static string NormalizeShoppingUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return "";
        return unit.Trim().ToLowerInvariant() switch
        {
            "tsp" or "teaspoon" or "teaspoons" or "tbsp" or "tablespoon" or "tablespoons" or "cup" or "cups" or "fl oz" or "fluid ounce" or "fluid ounces" or "ml" or "milliliter" or "milliliters" or "l" or "liter" or "liters" => "tsp",
            "oz" or "ounce" or "ounces" or "lb" or "lbs" or "pound" or "pounds" or "g" or "gram" or "grams" or "kg" or "kilogram" or "kilograms" => "g",
            "each" or "ea" or "item" or "items" => "each",
            var other => other
        };
    }

    private static decimal ToBaseShoppingQuantity(decimal quantity, string? rawUnit)
    {
        var unit = rawUnit?.Trim().ToLowerInvariant() ?? "";
        return unit switch
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
    }

    private static string FormatShoppingLine(decimal quantity, string unit, string name)
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
        else if (unit == "ml")
        {
            if (quantity >= 1000m) { displayUnit = "l"; displayQuantity = quantity / 1000m; }
            else { displayUnit = "ml"; displayQuantity = quantity; }
        }
        else { displayUnit = unit; displayQuantity = quantity; }

        return string.Join(" ", new[] { FormatShoppingQuantity(displayQuantity), displayUnit, name }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string FormatShoppingQuantity(decimal quantity)
        => quantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<MealPlan> LoadOrCreateAsync(DateTime weekStart, CancellationToken ct)
    {
        var plan = await _db.MealPlans
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.FamilyId == _current.FamilyId && p.WeekStartUtc == weekStart, ct);
        if (plan is not null) return plan;

        plan = MealPlan.Create(_current.FamilyId!.Value, weekStart);
        _db.MealPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan;
    }

    private void EnsureAuth()
    {
        if (!_current.IsAuthenticated || _current.FamilyId is null)
            throw new UnauthorizedException();
    }

    private static MealPlanDto ToDto(MealPlan plan) => new(
        plan.Id,
        plan.WeekStartUtc,
        plan.Notes,
        plan.Entries.Where(e => !e.IsDeleted)
            .OrderBy(e => e.DayUtc)
            .ThenBy(e => e.Slot)
            .Select(ToEntryDto)
            .ToList());

    private static MealEntryDto ToEntryDto(MealEntry e) => new(
        e.Id, e.DayUtc, e.Slot.ToString(), e.DishName, e.Notes, e.IngredientsCsv, e.RecipeId);
}
