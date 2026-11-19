using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Authenticated account and quiet-hours preferences.</summary>
public sealed class MeApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public MeApiClient(HttpClient http, IAuthSessionService auth) { _http = http; _auth = auth; }

    public Task<MeDto> GetAsync(CancellationToken ct = default)
        => SendAsync<MeDto>(HttpMethod.Get, "api/me", null, ct);

    public Task<QuietHoursDto> GetQuietHoursAsync(CancellationToken ct = default)
        => SendAsync<QuietHoursDto>(HttpMethod.Get, "api/me/quiet-hours", null, ct);

    public Task<QuietHoursDto> SetQuietHoursAsync(bool enabled, int? startMinutes, int? endMinutes, CancellationToken ct = default)
        => SendAsync<QuietHoursDto>(HttpMethod.Put, "api/me/quiet-hours",
            new StringContent(JsonSerializer.Serialize(new UpdateQuietHoursBody(enabled, enabled ? startMinutes : null, enabled ? endMinutes : null), JsonOptions), Encoding.UTF8, "application/json"), ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to manage account settings.");
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
                HttpStatusCode.Forbidden => "Your account cannot access these settings.",
                _ => $"FamilyOS could not update settings (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("The FamilyOS settings API returned an empty response.");
    }
}

public sealed record MeDto(string? ExternalIdentityId, Guid? UserId, Guid? FamilyId, Guid? MemberId, string? Role);
public sealed record QuietHoursDto(bool Enabled, int? StartMinutes, int? EndMinutes, string? StartLabel, string? EndLabel);
public sealed record UpdateQuietHoursBody(bool Enabled, int? StartMinutes, int? EndMinutes);
