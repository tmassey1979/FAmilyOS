using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Authenticated access to the existing request workflow endpoints.</summary>
public sealed class RequestsApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public RequestsApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public Task<List<FamilyRequestDto>> GetMineAsync(CancellationToken ct = default)
        => SendAsync<List<FamilyRequestDto>>(HttpMethod.Get, "api/requests/mine", null, ct);

    public Task<List<FamilyRequestDto>> GetApprovalQueueAsync(CancellationToken ct = default)
        => SendAsync<List<FamilyRequestDto>>(HttpMethod.Get, "api/requests/approval-queue", null, ct);

    public Task<FamilyRequestDto> RunActionAsync(Guid id, string action, string? reason = null, CancellationToken ct = default)
    {
        var body = action switch
        {
            "approve" => JsonSerializer.Serialize(new { conditional = false }, JsonOptions),
            "deny" => JsonSerializer.Serialize(new { reason = string.IsNullOrWhiteSpace(reason) ? "Not approved" : reason }, JsonOptions),
            "cancel" => JsonSerializer.Serialize(new { reason }, JsonOptions),
            _ => "{}"
        };
        var allowed = new[] { "submit", "approve", "deny", "cancel" };
        if (!allowed.Contains(action, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(action), "Unsupported request action.");
        var content = action == "submit" ? null : new StringContent(body, Encoding.UTF8, "application/json");
        return SendAsync<FamilyRequestDto>(HttpMethod.Post, $"api/requests/{id:D}/{action}", content, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to view family requests.");

        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session has expired. Sign in again.",
                HttpStatusCode.Forbidden => "Your family role does not allow that request action.",
                HttpStatusCode.NotFound => "That request could not be found.",
                _ => $"FamilyOS could not complete the request action (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token);
        return result ?? throw new InvalidOperationException("The FamilyOS API returned an empty request response.");
    }
}

public sealed record FamilyRequestDto(
    Guid Id, string Type, string Status, string Title, string? Summary,
    decimal? Amount, DateTime? NeededByUtc, Guid RequesterMemberId, string RequesterName,
    Guid? CurrentApproverMemberId, string? DenialReason, DateTime CreatedAtUtc);
