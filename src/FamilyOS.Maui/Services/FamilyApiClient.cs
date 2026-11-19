using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Reads household details using the authenticated FamilyOS API.</summary>
public sealed class FamilyApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public FamilyApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<FamilyDto> GetFamilyAsync(CancellationToken ct = default)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to view your family.");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiBaseUri, "api/family"));
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
                HttpStatusCode.Forbidden => "Your account is not currently linked to a family.",
                HttpStatusCode.NotFound => "Your family could not be found.",
                _ => $"FamilyOS could not load your family (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<FamilyDto>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("The FamilyOS API returned an empty family response.");
    }
}

public sealed record FamilyDto(Guid Id, string Name, string? TimeZone, string? Currency, List<FamilyMemberDto> Members);
public sealed record FamilyMemberDto(Guid Id, Guid UserId, string DisplayName, string Role, bool IsActive);
