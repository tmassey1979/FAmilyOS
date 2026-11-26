using FamilyOS.Application.Meals;
using FamilyOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Api.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize(Policy = FamilyAuthPolicies.FamilyMember)]
public class RecipesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RecipesController> _logger;

    public RecipesController(IMediator mediator, ILogger<RecipesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<RecipeDto>>> List(CancellationToken ct)
        => Ok(await _mediator.Send(new ListRecipesQuery(), ct));

    [HttpGet("page")]
    public async Task<ActionResult<RecipePageDto>> ListPage(
        [FromQuery] int skip = 0, [FromQuery] int take = 24, CancellationToken ct = default)
        => Ok(await _mediator.Send(new ListRecipesPageQuery(skip, take), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRecipeQuery(id), ct));

    [HttpGet("{id:guid}/scale")]
    public async Task<ActionResult<ScaledRecipeDto>> Scale(Guid id, [FromQuery] int servings, CancellationToken ct)
        => Ok(await _mediator.Send(new ScaleRecipeQuery(id, servings), ct));

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create([FromBody] RecipeBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new CreateRecipeCommand(
            body.Name, body.Servings ?? 4, body.IngredientsCsv, body.Instructions, body.Description, body.ImageUrl, body.SourceUrl, body.PrepMinutes, body.CookMinutes, body.Cuisine, body.Difficulty), ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Update(Guid id, [FromBody] RecipeBody body, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateRecipeCommand(
            id, body.Name, body.Servings ?? 4, body.IngredientsCsv, body.Instructions, body.Description, body.ImageUrl, body.SourceUrl, body.PrepMinutes, body.CookMinutes, body.Cuisine, body.Difficulty), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteRecipeCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<MealEntryDto>> Apply(Guid id, [FromBody] ApplyRecipeBody body, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new ApplyRecipeToMealCommand(
                id, body.DayUtc, body.Slot, body.WeekStartUtc), ct));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var failedEntries = ex.Entries.Select(entry =>
            {
                var key = entry.Metadata.FindPrimaryKey();
                var keyValues = key is null
                    ? "unknown"
                    : string.Join(", ", key.Properties.Select(property =>
                        $"{property.Name}={entry.Property(property.Name).CurrentValue ?? "null"}"));
                return $"{entry.Metadata.ClrType.Name} [{keyValues}] state={entry.State}";
            }).ToArray();

            _logger.LogError(ex,
                "Applying recipe {RecipeId} to {DayUtc}/{Slot} hit an EF concurrency failure. Entries: {Entries}",
                id, body.DayUtc, body.Slot, string.Join("; ", failedEntries));
            throw;
        }
    }

    [HttpPost("{id:guid}/to-shopping")]
    public async Task<ActionResult<object>> ToShopping(
        Guid id, [FromBody] ToShoppingBody? body, CancellationToken ct)
    {
        var added = await _mediator.Send(
            new PushRecipeIngredientsToQueueCommand(id, body?.TargetServings), ct);
        return Ok(new { added, targetServings = body?.TargetServings });
    }

    public record RecipeBody(
        string Name,
        int? Servings = 4,
        string? IngredientsCsv = null,
        string? Instructions = null, string? Description = null, string? ImageUrl = null, string? SourceUrl = null,
        int? PrepMinutes = null, int? CookMinutes = null, string? Cuisine = null, string? Difficulty = null);

    public record ApplyRecipeBody(
        DateTime DayUtc,
        string Slot,
        DateTime? WeekStartUtc = null);

    public record ToShoppingBody(int? TargetServings = null);
}
