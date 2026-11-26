using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Authenticated meal planning and recipe operations using existing API contracts.</summary>
public sealed class MealsApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public MealsApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public Task<MealPlanDto> GetWeekAsync(DateTime weekStartUtc, CancellationToken ct = default)
        => SendAsync<MealPlanDto>(HttpMethod.Get,
            $"api/meals/week?weekStart={Uri.EscapeDataString(weekStartUtc.ToUniversalTime().ToString("O"))}", null, ct);

    public Task<MealEntryDto> UpsertEntryAsync(MealEntryWriteDto body, CancellationToken ct = default)
        => SendAsync<MealEntryDto>(HttpMethod.Put, "api/meals/entries", JsonContent(body), ct);

    public Task RemoveEntryAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/meals/entries/{id:D}", ct);

    public Task<MealShoppingResultDto> AddWeekToShoppingAsync(DateTime weekStartUtc, CancellationToken ct = default)
        => SendAsync<MealShoppingResultDto>(HttpMethod.Post,
            $"api/meals/week/to-shopping?weekStart={Uri.EscapeDataString(weekStartUtc.ToUniversalTime().ToString("O"))}", null, ct);

    public Task<List<RecipeDto>> GetRecipesAsync(CancellationToken ct = default)
        => SendAsync<List<RecipeDto>>(HttpMethod.Get, "api/recipes", null, ct);

    public Task<List<IngredientCatalogDto>> SearchIngredientsAsync(string query, int limit = 25, CancellationToken ct = default)
        => SendAsync<List<IngredientCatalogDto>>(HttpMethod.Get,
            $"api/ingredients/search?q={Uri.EscapeDataString(query ?? string.Empty)}&limit={Math.Clamp(limit, 1, 50)}", null, ct);

    public Task<RecipeDto> CreateRecipeAsync(RecipeWriteDto body, CancellationToken ct = default)
        => SendAsync<RecipeDto>(HttpMethod.Post, "api/recipes", JsonContent(body), ct);

    public Task DeleteRecipeAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/recipes/{id:D}", ct);

    public Task<MealEntryDto> ApplyRecipeAsync(Guid id, ApplyRecipeDto body, CancellationToken ct = default)
        => SendAsync<MealEntryDto>(HttpMethod.Post, $"api/recipes/{id:D}/apply", JsonContent(body), ct);

    public Task<MealShoppingResultDto> AddRecipeToShoppingAsync(Guid id, int? targetServings = null, CancellationToken ct = default)
        => SendAsync<MealShoppingResultDto>(HttpMethod.Post, $"api/recipes/{id:D}/to-shopping",
            JsonContent(new RecipeShoppingDto(targetServings)), ct);

    private static StringContent JsonContent<T>(T value)
        => new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = await CreateRequestAsync(method, path, content, ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        await EnsureSuccessAsync(response, timeout.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("The FamilyOS meals API returned an empty response.");
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, CancellationToken ct)
    {
        using var request = await CreateRequestAsync(method, path, null, ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        await EnsureSuccessAsync(response, timeout.Token);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to use family meal planning.");
        var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Sign in again.",
            HttpStatusCode.Forbidden => "Your account cannot access this household's meal plan.",
            HttpStatusCode.NotFound => "That meal or recipe could not be found.",
            _ => $"FamilyOS could not complete the meal request (HTTP {(int)response.StatusCode})."
        };
        try
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(payload);
            foreach (var key in new[] { "detail", "error", "title" })
                if (document.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    message = value.GetString()!;
                    break;
                }
        }
        catch (JsonException) { }
        throw new FamilyApiException(response.StatusCode, message);
    }
}

public sealed record MealPlanDto(Guid Id, DateTime WeekStartUtc, string? Notes, List<MealEntryDto> Entries);
public sealed record MealEntryDto(Guid Id, DateTime DayUtc, string Slot, string DishName, string? Notes, string? IngredientsCsv);
public sealed record MealEntryWriteDto(DateTime DayUtc, string Slot, string DishName, string? Notes = null, string? IngredientsCsv = null, DateTime? WeekStartUtc = null);
public sealed record RecipeDto(Guid Id, string Name, int Servings, string? IngredientsCsv, string? Instructions, bool IsBuiltIn = false, List<string>? Tags = null);
public sealed record RecipeWriteDto(string Name, int? Servings = 4, string? IngredientsCsv = null, string? Instructions = null);
public sealed record ApplyRecipeDto(DateTime DayUtc, string Slot, DateTime? WeekStartUtc = null);
public sealed record RecipeShoppingDto(int? TargetServings = null);
public sealed record MealShoppingResultDto(int Added, int? TargetServings = null);\npublic sealed record IngredientCatalogDto(Guid Id, string Name, string Category, string? AliasesCsv, bool IsBuiltIn);
