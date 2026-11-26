using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Meals;
using FamilyOS.Domain.Procurement;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Application.Meals;

public record RecipeDto(
    Guid Id, string Name, int Servings, string? IngredientsCsv, string? Instructions, bool IsBuiltIn = false, List<string>? Tags = null, string? Description = null, string? ImageUrl = null, string? SourceUrl = null, int? PrepMinutes = null, int? CookMinutes = null, string? Cuisine = null, string? Difficulty = null, List<string>? IngredientNames = null);

public record ListRecipesQuery() : IRequest<List<RecipeDto>>;
public record ListRecipesPageQuery(int Skip = 0, int Take = 24) : IRequest<RecipePageDto>;
public record RecipePageDto(List<RecipeDto> Items, int TotalCount, int Skip, int Take);
public record GetRecipeQuery(Guid Id) : IRequest<RecipeDto>;
public record CreateRecipeCommand(
    string Name, int Servings = 4, string? IngredientsCsv = null, string? Instructions = null, string? Description = null, string? ImageUrl = null, string? SourceUrl = null, int? PrepMinutes = null, int? CookMinutes = null, string? Cuisine = null, string? Difficulty = null) : IRequest<RecipeDto>;
public record UpdateRecipeCommand(
    Guid Id, string Name, int Servings, string? IngredientsCsv, string? Instructions, string? Description = null, string? ImageUrl = null, string? SourceUrl = null, int? PrepMinutes = null, int? CookMinutes = null, string? Cuisine = null, string? Difficulty = null) : IRequest<RecipeDto>;
public record DeleteRecipeCommand(Guid Id) : IRequest;
public record ApplyRecipeToMealCommand(
    Guid RecipeId, DateTime DayUtc, string Slot, DateTime? WeekStartUtc = null) : IRequest<MealEntryDto>;
/// <param name="TargetServings">When set, scale numeric ingredient qtys from recipe servings.</param>
public record PushRecipeIngredientsToQueueCommand(Guid RecipeId, int? TargetServings = null) : IRequest<int>;
public record ScaleRecipeQuery(Guid Id, int TargetServings) : IRequest<ScaledRecipeDto>;
public record ScaledRecipeDto(Guid Id, string Name, int BaseServings, int TargetServings, IReadOnlyList<string> Ingredients);

public class RecipeCommandHandlers :
    IRequestHandler<ListRecipesQuery, List<RecipeDto>>,
    IRequestHandler<ListRecipesPageQuery, RecipePageDto>,
    IRequestHandler<GetRecipeQuery, RecipeDto>,
    IRequestHandler<CreateRecipeCommand, RecipeDto>,
    IRequestHandler<UpdateRecipeCommand, RecipeDto>,
    IRequestHandler<DeleteRecipeCommand>,
    IRequestHandler<ApplyRecipeToMealCommand, MealEntryDto>,
    IRequestHandler<PushRecipeIngredientsToQueueCommand, int>,
    IRequestHandler<ScaleRecipeQuery, ScaledRecipeDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ILogger<RecipeCommandHandlers> _logger;

    public RecipeCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current, ILogger<RecipeCommandHandlers> logger)
    {
        _db = db;
        _current = current;
        _logger = logger;
    }

    public async Task<List<RecipeDto>> Handle(ListRecipesQuery query, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var list = await _db.Recipes.AsNoTracking()
            .Where(r => r.FamilyId == _current.FamilyId || r.FamilyId == Guid.Empty)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        return await AddTagsAsync(list, ct);
    }

    public async Task<RecipePageDto> Handle(ListRecipesPageQuery query, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, 48);
        var recipes = _db.Recipes.AsNoTracking()
            .Where(r => r.FamilyId == _current.FamilyId || r.FamilyId == Guid.Empty);
        var total = await recipes.CountAsync(ct);
        var page = await recipes.OrderBy(r => r.Name)
            .Skip(skip).Take(take).ToListAsync(ct);
        var items = await AddTagsAsync(page, ct);
        return new RecipePageDto(items, total, skip, take);
    }

    public async Task<RecipeDto> Handle(GetRecipeQuery query, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var r = await _db.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.Id && (x.FamilyId == _current.FamilyId || x.FamilyId == Guid.Empty), ct)
            ?? throw new NotFoundException("Recipe", query.Id);
        var tagged = await AddTagsAsync(new List<Recipe> { r }, ct);
        return tagged[0];
    }

    public async Task<RecipeDto> Handle(CreateRecipeCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = Recipe.Create(
            _current.FamilyId!.Value, cmd.Name, cmd.Servings, cmd.IngredientsCsv, cmd.Instructions, cmd.Description, cmd.ImageUrl, cmd.SourceUrl, cmd.PrepMinutes, cmd.CookMinutes, cmd.Cuisine, cmd.Difficulty);
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync(ct);
        return ToDto(recipe);
    }

    public async Task<RecipeDto> Handle(UpdateRecipeCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = await _db.Recipes
            .FirstOrDefaultAsync(x => x.Id == cmd.Id && x.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Recipe", cmd.Id);
        recipe.Update(cmd.Name, cmd.Servings, cmd.IngredientsCsv, cmd.Instructions, cmd.Description, cmd.ImageUrl, cmd.SourceUrl, cmd.PrepMinutes, cmd.CookMinutes, cmd.Cuisine, cmd.Difficulty);
        await _db.SaveChangesAsync(ct);
        return ToDto(recipe);
    }

    public async Task Handle(DeleteRecipeCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = await _db.Recipes
            .FirstOrDefaultAsync(x => x.Id == cmd.Id && x.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Recipe", cmd.Id);
        recipe.SoftDelete();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MealEntryDto> Handle(ApplyRecipeToMealCommand cmd, CancellationToken ct)
    {
        var operationId = Guid.NewGuid();
        var startedAt = System.Diagnostics.Stopwatch.StartNew();
        await EnsureFamilyAsync(ct);
        _logger.LogInformation(
            "Recipe apply {OperationId} started. RecipeId={RecipeId}, FamilyId={FamilyId}, MemberId={MemberId}, DayUtc={DayUtc:o}, RequestedWeekStartUtc={WeekStartUtc:o}, Slot={Slot}",
            operationId, cmd.RecipeId, _current.FamilyId, _current.MemberId, cmd.DayUtc, cmd.WeekStartUtc, cmd.Slot);

        if (!Enum.TryParse<MealSlot>(cmd.Slot, ignoreCase: true, out var slot))
            throw new ValidationException("Slot", "Slot must be Breakfast, Lunch, Dinner, or Snack.");

        var recipe = await _db.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cmd.RecipeId && (x.FamilyId == _current.FamilyId || x.FamilyId == Guid.Empty), ct)
            ?? throw new NotFoundException("Recipe", cmd.RecipeId);
        _logger.LogInformation(
            "Recipe apply {OperationId}: recipe loaded. RecipeId={RecipeId}, RecipeFamilyId={RecipeFamilyId}, Name={RecipeName}",
            operationId, recipe.Id, recipe.FamilyId, recipe.Name);

        var week = MealPlan.ToMonday(cmd.WeekStartUtc ?? cmd.DayUtc);
        var plan = await _db.MealPlans
            .Include(p => p.Entries)
            .FirstOrDefaultAsync(p => p.FamilyId == _current.FamilyId && p.WeekStartUtc == week, ct);

        if (plan is null)
        {
            _logger.LogInformation("Recipe apply {OperationId}: no meal plan found; creating one for {WeekStartUtc:o}", operationId, week);
            plan = MealPlan.Create(_current.FamilyId!.Value, week);
            _db.MealPlans.Add(plan);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Recipe apply {OperationId}: new meal plan persisted. MealPlanId={MealPlanId}", operationId, plan.Id);
        }
        else
        {
            _logger.LogInformation(
                "Recipe apply {OperationId}: meal plan loaded. MealPlanId={MealPlanId}, WeekStartUtc={WeekStartUtc:o}, EntryCount={EntryCount}, Entries={Entries}",
                operationId, plan.Id, plan.WeekStartUtc, plan.Entries.Count,
                string.Join("; ", plan.Entries.Select(e => $"{e.Id}|{e.DayUtc:o}|{e.Slot}|deleted={e.IsDeleted}|recipe={e.RecipeId}")));
        }

        var targetDay = cmd.DayUtc.Date;
        var matchedEntryBeforeUpsert = plan.Entries.FirstOrDefault(e =>
            !e.IsDeleted && e.DayUtc.Date == targetDay && e.Slot == slot);
        _logger.LogInformation(
            "Recipe apply {OperationId}: target match before upsert. Found={Found}, EntryId={EntryId}, IsDeleted={IsDeleted}",
            operationId, matchedEntryBeforeUpsert is not null, matchedEntryBeforeUpsert?.Id, matchedEntryBeforeUpsert?.IsDeleted);

        var entry = plan.UpsertEntry(
            cmd.DayUtc, slot, recipe.Name, notes: $"{recipe.Servings} servings", ingredientsCsv: recipe.IngredientsCsv, recipeId: recipe.Id);

        _logger.LogInformation(
            "Recipe apply {OperationId}: aggregate upsert complete. EntryId={EntryId}, MealPlanId={MealPlanId}, FamilyId={FamilyId}, DayUtc={DayUtc:o}, Slot={Slot}, DishName={DishName}, IsDeleted={IsDeleted}, WasExisting={WasExisting}",
            operationId, entry.Id, entry.MealPlanId, entry.FamilyId, entry.DayUtc, entry.Slot, entry.DishName, entry.IsDeleted, matchedEntryBeforeUpsert is not null);

        // Query the database directly (ignoring soft-delete filters) before SaveChanges.
        // This distinguishes a new child from a stale/deleted child being updated.
        var persistedBeforeSave = await _db.MealEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Id == entry.Id)
            .Select(e => new { e.Id, e.MealPlanId, e.FamilyId, e.DayUtc, e.Slot, e.IsDeleted, e.UpdatedAtUtc })
            .SingleOrDefaultAsync(ct);
        _logger.LogInformation(
            "Recipe apply {OperationId}: target row immediately before save. Exists={Exists}, Persisted={Persisted}",
            operationId, persistedBeforeSave is not null,
            persistedBeforeSave is null ? "none" :
                $"id={persistedBeforeSave.Id};plan={persistedBeforeSave.MealPlanId};family={persistedBeforeSave.FamilyId};day={persistedBeforeSave.DayUtc:o};slot={persistedBeforeSave.Slot};deleted={persistedBeforeSave.IsDeleted};updated={persistedBeforeSave.UpdatedAtUtc:o}");

        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Recipe apply {OperationId} saved successfully. EntryId={EntryId}, WasExisting={WasExisting}, ElapsedMs={ElapsedMs}",
                operationId, entry.Id, matchedEntryBeforeUpsert is not null, startedAt.Elapsed.TotalMilliseconds);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            string postFailureState;
            try
            {
                var rowAfterFailure = await _db.MealEntries.IgnoreQueryFilters().AsNoTracking()
                    .Where(e => e.Id == entry.Id)
                    .Select(e => new { e.Id, e.MealPlanId, e.FamilyId, e.DayUtc, e.Slot, e.IsDeleted, e.UpdatedAtUtc })
                    .SingleOrDefaultAsync(ct);
                postFailureState = rowAfterFailure is null ? "row-missing" :
                    $"row-present;plan={rowAfterFailure.MealPlanId};family={rowAfterFailure.FamilyId};day={rowAfterFailure.DayUtc:o};slot={rowAfterFailure.Slot};deleted={rowAfterFailure.IsDeleted};updated={rowAfterFailure.UpdatedAtUtc:o}";
            }
            catch (Exception diagnosticException)
            {
                postFailureState = $"diagnostic-query-failed:{diagnosticException.GetType().Name}:{diagnosticException.Message}";
            }

            _logger.LogError(ex,
                "Recipe apply {OperationId} concurrency failure. RecipeId={RecipeId}, FamilyId={FamilyId}, MealPlanId={MealPlanId}, EntryId={EntryId}, WasExisting={WasExisting}, PersistedBeforeSave={PersistedBeforeSave}, StateAfterFailure={StateAfterFailure}, ElapsedMs={ElapsedMs}, FailedEntries={FailedEntries}",
                operationId, recipe.Id, _current.FamilyId, plan.Id, entry.Id, matchedEntryBeforeUpsert is not null,
                persistedBeforeSave is null ? "missing" : $"present;deleted={persistedBeforeSave.IsDeleted};plan={persistedBeforeSave.MealPlanId}",
                postFailureState, startedAt.Elapsed.TotalMilliseconds,
                string.Join("; ", ex.Entries.Select(e => $"{e.Metadata.ClrType.Name}:{e.State}")));
            throw;
        }

        return new MealEntryDto(
            entry.Id, entry.DayUtc, entry.Slot.ToString(), entry.DishName, entry.Notes, entry.IngredientsCsv, entry.RecipeId);
    }

    public async Task<int> Handle(PushRecipeIngredientsToQueueCommand cmd, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = await _db.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cmd.RecipeId && (x.FamilyId == _current.FamilyId || x.FamilyId == Guid.Empty), ct)
            ?? throw new NotFoundException("Recipe", cmd.RecipeId);

        var targetServings = cmd.TargetServings is int requested ? Math.Clamp(requested, 1, 100) : recipe.Servings;
        var factor = (decimal)targetServings / recipe.Servings;
        var structuredRows = await (from row in _db.RecipeIngredients.AsNoTracking()
                                    join ingredient in _db.IngredientCatalog.AsNoTracking() on row.IngredientId equals ingredient.Id
                                    where row.RecipeId == recipe.Id
                                    orderby row.SortOrder
                                    select new { row.IngredientId, ingredient.Name, row.Quantity, row.Unit }).ToListAsync(ct);
        var names = new List<string>();
        if (structuredRows.Count > 0)
        {
            var aggregate = new Dictionary<(Guid IngredientId, string Unit), (string Name, decimal? Quantity, string Unit)>();
            foreach (var row in structuredRows)
            {
                var unit = NormalizeShoppingUnit(row.Unit);
                decimal? quantity = row.Quantity is null ? null : ToBaseShoppingQuantity(row.Quantity.Value, row.Unit) * factor;
                var key = (row.IngredientId, unit);
                if (aggregate.TryGetValue(key, out var current))
                    aggregate[key] = (current.Name, current.Quantity is null || quantity is null
                        ? null : current.Quantity.Value + quantity.Value, current.Unit);
                else aggregate[key] = (row.Name, quantity, unit);
            }
            names.AddRange(aggregate.Values.Select(x => x.Quantity is null
                ? x.Name : FormatShoppingLine(x.Quantity.Value, x.Unit, x.Name)));
        }
        else
        {
            names.AddRange(targetServings == recipe.Servings
                ? recipe.IngredientList()
                : recipe.ScaleIngredientsForServings(targetServings));
        }
        names = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
                category: "Recipe"));
            existingSet.Add(name);
            added++;
        }

        if (added > 0)
            await _db.SaveChangesAsync(ct);
        return added;
    }

    public async Task<ScaledRecipeDto> Handle(ScaleRecipeQuery query, CancellationToken ct)
    {
        await EnsureFamilyAsync(ct);
        var recipe = await _db.Recipes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.Id && (x.FamilyId == _current.FamilyId || x.FamilyId == Guid.Empty), ct)
            ?? throw new NotFoundException("Recipe", query.Id);
        var ingredients = recipe.ScaleIngredientsForServings(query.TargetServings);
        return new ScaledRecipeDto(recipe.Id, recipe.Name, recipe.Servings, query.TargetServings, ingredients);
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
        => rawUnit?.Trim().ToLowerInvariant() switch
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
        else { displayUnit = unit; displayQuantity = quantity; }
        return string.Join(" ", new[] { displayQuantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), displayUnit, name }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private async Task<List<RecipeDto>> AddTagsAsync(List<Recipe> recipes, CancellationToken ct)
    {
        if (recipes.Count == 0) return new List<RecipeDto>();
        var ids = recipes.Select(x => x.Id).ToArray();
        var tagRows = await (from link in _db.RecipeTagLinks.AsNoTracking()
                             join tag in _db.RecipeTags.AsNoTracking() on link.TagId equals tag.Id
                             where ids.Contains(link.RecipeId)
                             select new { link.RecipeId, tag.Name }).ToListAsync(ct);
        var tagsByRecipe = tagRows.GroupBy(x => x.RecipeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(x => x).ToList());
        var ingredientRows = await (from row in _db.RecipeIngredients.AsNoTracking()
                                    join ingredient in _db.IngredientCatalog.AsNoTracking() on row.IngredientId equals ingredient.Id
                                    where ids.Contains(row.RecipeId)
                                    select new { row.RecipeId, ingredient.Name }).ToListAsync(ct);
        var ingredientsByRecipe = ingredientRows.GroupBy(x => x.RecipeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).Distinct().OrderBy(x => x).ToList());
        return recipes.Select(r => ToDto(r) with
        {
            Tags = tagsByRecipe.TryGetValue(r.Id, out var tags) ? tags : new List<string>(),
            IngredientNames = ingredientsByRecipe.TryGetValue(r.Id, out var ingredientNames) ? ingredientNames : new List<string>()
        }).ToList();
    }

    private async Task EnsureFamilyAsync(CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || _current.FamilyId is null)
            throw new UnauthorizedException();
    }

    private static RecipeDto ToDto(Recipe r) =>
        new(r.Id, r.Name, r.Servings, r.IngredientsCsv, r.Instructions, r.FamilyId == Guid.Empty, null, r.Description, r.ImageUrl, r.SourceUrl, r.PrepMinutes, r.CookMinutes, r.Cuisine, r.Difficulty);
}
