using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Meals;
using FamilyOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public sealed class StructuredRecipesController : ControllerBase
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public StructuredRecipesController(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    [HttpPost("structured")]
    public async Task<ActionResult<StructuredRecipeDto>> Save(
        [FromBody] SaveStructuredRecipeBody body, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || _current.FamilyId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest("Recipe name is required.");
        if (body.Servings is < 1 or > 100) return BadRequest("Servings must be between 1 and 100.");
        if (body.Ingredients.Count > 100 || body.Steps.Count > 50 || body.Tags.Count > 20
            || body.Tags.Any(x => x?.Length > 60))
            return BadRequest("This recipe has too many ingredients, steps, or tags.");

        if (body.Ingredients is null || body.Steps is null || body.Tags is null)
            return BadRequest("Ingredients, steps, and tags must be supplied.");
        Recipe recipe;
        if (body.Id is Guid id)
        {
            recipe = await _db.Recipes.FirstOrDefaultAsync(
                x => x.Id == id && x.FamilyId == _current.FamilyId, ct)
                ?? throw new InvalidOperationException("Recipe not found.");
            recipe.Update(body.Name, body.Servings, null,
                string.Join(Environment.NewLine, body.Steps.Select(x => x.Text)), body.Description, body.ImageUrl, body.SourceUrl, body.PrepMinutes, body.CookMinutes, body.Cuisine, body.Difficulty);
            _db.RecipeIngredients.RemoveRange(await _db.RecipeIngredients.IgnoreQueryFilters().Where(x => x.RecipeId == id).ToListAsync(ct));
            _db.RecipeInstructionSteps.RemoveRange(await _db.RecipeInstructionSteps.IgnoreQueryFilters().Where(x => x.RecipeId == id).ToListAsync(ct));
            _db.RecipeTagLinks.RemoveRange(await _db.RecipeTagLinks.IgnoreQueryFilters().Where(x => x.RecipeId == id).ToListAsync(ct));
        }
        else
        {
            recipe = Recipe.Create(_current.FamilyId.Value, body.Name, body.Servings, null,
                string.Join(Environment.NewLine, body.Steps.Select(x => x.Text)), body.Description, body.ImageUrl, body.SourceUrl, body.PrepMinutes, body.CookMinutes, body.Cuisine, body.Difficulty);
            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync(ct);
        }

        var ingredientIds = body.Ingredients.Select(x => x.IngredientId).Distinct().ToArray();
        var allowedIngredients = await _db.IngredientCatalog
            .Where(x => ingredientIds.Contains(x.Id)
                && (x.FamilyId == null || x.FamilyId == _current.FamilyId))
            .Select(x => x.Id).ToListAsync(ct);
        if (allowedIngredients.Count != ingredientIds.Length)
            return BadRequest("One or more selected ingredients are not available to this family.");

        foreach (var item in body.Ingredients.OrderBy(x => x.SortOrder))
            _db.RecipeIngredients.Add(RecipeIngredient.Create(recipe.Id, item.IngredientId,
                item.Quantity, item.Unit, item.PreparationNote, item.SortOrder, item.Section));

        for (var i = 0; i < body.Steps.Count; i++)
            _db.RecipeInstructionSteps.Add(RecipeInstructionStep.Create(
                recipe.Id, i + 1, body.Steps[i].Text, body.Steps[i].DurationMinutes));

        foreach (var rawTag in body.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var slug = string.Join('-', rawTag.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var tag = await _db.RecipeTags.FirstOrDefaultAsync(
                x => x.Slug == slug && (x.FamilyId == null || x.FamilyId == _current.FamilyId), ct);
            if (tag is null)
            {
                tag = RecipeTag.CreateFamily(_current.FamilyId.Value, rawTag);
                _db.RecipeTags.Add(tag);
                await _db.SaveChangesAsync(ct);
            }
            _db.RecipeTagLinks.Add(RecipeTagLink.Create(recipe.Id, tag.Id));
        }

        await _db.SaveChangesAsync(ct);
        return Ok(await ReadRecipeAsync(recipe.Id, ct));
    }

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<StructuredRecipeDto>> Details(Guid id, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || _current.FamilyId is null) return Unauthorized();
        var recipe = await _db.Recipes.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == id && (x.FamilyId == _current.FamilyId || x.FamilyId == Guid.Empty), ct);
        if (recipe is null) return NotFound();
        return Ok(await ReadRecipeAsync(id, ct));
    }

    private async Task<StructuredRecipeDto> ReadRecipeAsync(Guid id, CancellationToken ct)
    {
        var recipe = await _db.Recipes.AsNoTracking().FirstAsync(x => x.Id == id, ct);
        var ingredients = await (from row in _db.RecipeIngredients.AsNoTracking()
                                 join ingredient in _db.IngredientCatalog.AsNoTracking() on row.IngredientId equals ingredient.Id
                                 where row.RecipeId == id
                                 orderby row.SortOrder
                                 select new StructuredRecipeIngredientDto(
                                     row.IngredientId, ingredient.Name, ingredient.Category, row.Quantity,
                                     row.Unit, row.PreparationNote, row.Section, row.SortOrder)).ToListAsync(ct);
        var steps = await _db.RecipeInstructionSteps.AsNoTracking()
            .Where(x => x.RecipeId == id).OrderBy(x => x.StepNumber)
            .Select(x => new StructuredRecipeStepDto(x.StepNumber, x.Text, x.DurationMinutes)).ToListAsync(ct);
        var tags = await (from link in _db.RecipeTagLinks.AsNoTracking()
                          join tag in _db.RecipeTags.AsNoTracking() on link.TagId equals tag.Id
                          where link.RecipeId == id
                          select tag.Name).ToListAsync(ct);
        return new StructuredRecipeDto(recipe.Id, recipe.Name, recipe.Servings, recipe.FamilyId == Guid.Empty,
            recipe.IngredientsCsv, ingredients, steps, tags, recipe.Description, recipe.ImageUrl, recipe.SourceUrl, recipe.PrepMinutes, recipe.CookMinutes, recipe.Cuisine, recipe.Difficulty);
    }
}

public sealed record SaveStructuredRecipeBody(
    Guid? Id, string Name, int Servings,
    List<SaveRecipeIngredientBody> Ingredients,
    List<SaveRecipeStepBody> Steps,
    List<string> Tags,
    string? Description = null, string? ImageUrl = null, string? SourceUrl = null,
    int? PrepMinutes = null, int? CookMinutes = null, string? Cuisine = null, string? Difficulty = null);

public sealed record SaveRecipeIngredientBody(
    Guid IngredientId, decimal? Quantity, string? Unit, string? PreparationNote, string? Section, int SortOrder);

public sealed record SaveRecipeStepBody(string Text, int? DurationMinutes = null);
public sealed record StructuredRecipeIngredientDto(
    Guid IngredientId, string Name, string Category, decimal? Quantity, string? Unit,
    string? PreparationNote, string? Section, int SortOrder);
public sealed record StructuredRecipeStepDto(int StepNumber, string Text, int? DurationMinutes);
public sealed record StructuredRecipeDto(
    Guid Id, string Name, int Servings, bool IsBuiltIn, string? LegacyIngredientsCsv,
    List<StructuredRecipeIngredientDto> Ingredients, List<StructuredRecipeStepDto> Steps, List<string> Tags,
    string? Description, string? ImageUrl, string? SourceUrl, int? PrepMinutes, int? CookMinutes, string? Cuisine, string? Difficulty);
