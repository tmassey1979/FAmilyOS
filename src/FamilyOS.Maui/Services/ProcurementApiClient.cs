using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Household shopping and procurement API operations.</summary>
public sealed class ProcurementApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public ProcurementApiClient(HttpClient http, IAuthSessionService auth) { _http = http; _auth = auth; }

    public Task<List<ProcurementItemDto>> GetQueueAsync(CancellationToken ct = default)
        => SendAsync<List<ProcurementItemDto>>(HttpMethod.Get, "api/procurement/queue", null, ct);
    public Task<List<CartDto>> GetCartsAsync(CancellationToken ct = default)
        => SendAsync<List<CartDto>>(HttpMethod.Get, "api/procurement/carts", null, ct);
    public Task<ProcurementItemDto> CreateItemAsync(CreateQueueItemDto body, CancellationToken ct = default)
        => SendAsync<ProcurementItemDto>(HttpMethod.Post, "api/procurement/items", JsonContent(body), ct);
    public Task<CartDto> CreateCartAsync(string store, CancellationToken ct = default)
        => SendAsync<CartDto>(HttpMethod.Post, "api/procurement/carts", JsonContent(new { storeName = store }), ct);
    public Task<ProcurementItemDto> AddToCartAsync(Guid id, Guid cartId, CancellationToken ct = default)
        => SendAsync<ProcurementItemDto>(HttpMethod.Post, $"api/procurement/items/{id:D}/add-to-cart", JsonContent(new { cartId }), ct);
    public Task<ProcurementItemDto> HoldAsync(Guid id, CancellationToken ct = default)
        => SendAsync<ProcurementItemDto>(HttpMethod.Post, $"api/procurement/items/{id:D}/hold", null, ct);
    public Task<ProcurementItemDto> DeferAsync(Guid id, CancellationToken ct = default)
        => SendAsync<ProcurementItemDto>(HttpMethod.Post, $"api/procurement/items/{id:D}/defer", null, ct);
    public Task<ProcurementItemDto> NoLongerNeededAsync(Guid id, CancellationToken ct = default)
        => SendAsync<ProcurementItemDto>(HttpMethod.Post, $"api/procurement/items/{id:D}/no-longer-needed", null, ct);
    public Task<CartDto> AbandonCartAsync(Guid id, CancellationToken ct = default)
        => SendAsync<CartDto>(HttpMethod.Post, $"api/procurement/carts/{id:D}/abandon", null, ct);
    public Task<ProcurementItemDto> PurchaseAsync(Guid id, decimal price, string? store, CancellationToken ct = default)
        => SendAsync<ProcurementItemDto>(HttpMethod.Post, $"api/procurement/items/{id:D}/purchase", JsonContent(new { actualPrice = price, store }), ct);

    private static StringContent JsonContent<T>(T body) => new(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token)) throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to use household shopping.");
        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            var message = $"Shopping request failed (HTTP {(int)response.StatusCode}).";
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                foreach (var key in new[] { "detail", "error", "title" })
                    if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    { message = value.GetString()!; break; }
            }
            catch (JsonException) { }
            throw new FamilyApiException(response.StatusCode, message);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("The shopping API returned an empty response.");
    }
}

public sealed record ProcurementItemDto(Guid Id, string Name, string? Brand, string? Size, string? Category,
    decimal? Quantity, decimal? EstimatedPrice, decimal? ActualPrice, string? PreferredStore, string Status,
    DateTime CreatedAtUtc, Guid? RequestId = null, Guid? CartId = null);
public sealed record CartDto(Guid Id, string StoreName, List<ProcurementItemDto> Items, bool IsActive);
public sealed record CreateQueueItemDto(string Name, string? Brand = null, string? Size = null, string? Category = null,
    decimal? Quantity = 1, decimal? EstimatedPrice = null, string? PreferredStore = null, Guid? RequestId = null);
