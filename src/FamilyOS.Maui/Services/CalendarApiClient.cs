using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Authenticated access to the existing family calendar endpoints.</summary>
public sealed class CalendarApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public CalendarApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public Task<List<CalendarEventDto>> GetRangeAsync(DateTime fromLocal, DateTime toLocal, CancellationToken ct = default)
    {
        var from = Uri.EscapeDataString(fromLocal.ToUniversalTime().ToString("O"));
        var to = Uri.EscapeDataString(toLocal.ToUniversalTime().ToString("O"));
        return SendAsync<List<CalendarEventDto>>(HttpMethod.Get, $"api/calendar?from={from}&to={to}", null, ct);
    }

    public Task<CalendarEventDto> CreateAsync(CalendarEventWriteDto body, CancellationToken ct = default)
        => SendAsync<CalendarEventDto>(HttpMethod.Post, "api/calendar", JsonContent(body), ct);

    public Task<CalendarEventDto> UpdateAsync(Guid id, CalendarEventWriteDto body, CancellationToken ct = default)
        => SendAsync<CalendarEventDto>(HttpMethod.Put, $"api/calendar/{id:D}", JsonContent(body), ct);

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        await SendNoContentAsync(HttpMethod.Post, $"api/calendar/{id:D}/cancel", ct);
    }

    private static StringContent JsonContent<T>(T value)
        => new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to use the family calendar.");

        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        await EnsureSuccessAsync(response, timeout.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("The FamilyOS calendar API returned an empty response.");
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to use the family calendar.");

        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        await EnsureSuccessAsync(response, timeout.Token);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Sign in again.",
            HttpStatusCode.Forbidden => "Your account cannot make that calendar change.",
            HttpStatusCode.NotFound => "That calendar event could not be found.",
            _ => $"FamilyOS could not complete the calendar request (HTTP {(int)response.StatusCode})."
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

public sealed record CalendarEventDto(
    Guid Id, string Title, string? Description, DateTime StartUtc, DateTime? EndUtc,
    bool AllDay, string? Location, Guid? LinkedTaskId, Guid? LinkedRequestId);

public sealed record CalendarEventWriteDto(
    string Title, DateTime StartUtc, DateTime? EndUtc, bool AllDay, string? Location,
    string? Description = null, Guid? LinkedTaskId = null, Guid? LinkedRequestId = null);
