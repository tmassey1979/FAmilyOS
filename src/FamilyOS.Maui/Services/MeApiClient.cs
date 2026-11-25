using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Authenticated account, profile, and quiet-hours preferences.</summary>
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

    public Task<GoogleCalendarStatusDto> GetGoogleCalendarStatusAsync(CancellationToken ct = default)
        => SendAsync<GoogleCalendarStatusDto>(HttpMethod.Get, "api/me/google-calendar", null, ct);

    public Task<GoogleCalendarAuthUrlDto> GetGoogleCalendarAuthUrlAsync(CancellationToken ct = default)
        => SendAsync<GoogleCalendarAuthUrlDto>(HttpMethod.Get, "api/me/google-calendar/auth-url", null, ct);

    public Task<GoogleCalendarSyncDto> SyncGoogleCalendarAsync(CancellationToken ct = default)
        => SendAsync<GoogleCalendarSyncDto>(HttpMethod.Post, "api/me/google-calendar/sync", null, ct);

    public Task DisconnectGoogleCalendarAsync(CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, "api/me/google-calendar", ct);

    public Task<ProfileDto> GetProfileAsync(CancellationToken ct = default)
        => SendAsync<ProfileDto>(HttpMethod.Get, "api/me/profile", null, ct);

    public Task<ProfileDto> UpdateProfileAsync(string displayName, CancellationToken ct = default)
        => SendAsync<ProfileDto>(HttpMethod.Put, "api/me/profile",
            new StringContent(JsonSerializer.Serialize(new { displayName }, JsonOptions), Encoding.UTF8, "application/json"), ct);

    public async Task<ProfileDto> UploadAvatarAsync(Stream image, string fileName, string contentType, CancellationToken ct = default)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to upload a photo.");
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(image);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ApiBaseUri, "api/me/avatar")) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new FamilyApiException(response.StatusCode, $"Could not upload photo (HTTP {(int)response.StatusCode}).");
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<ProfileDto>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("Empty profile response.");
    }

    public Task<ProfileDto> ClearAvatarAsync(CancellationToken ct = default)
        => SendAsync<ProfileDto>(HttpMethod.Delete, "api/me/avatar", null, ct);

    private async Task SendNoContentAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to manage Google Calendar.");
        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Sign in again.",
            HttpStatusCode.Forbidden => "Your account cannot manage this integration.",
            _ => $"FamilyOS could not update Google Calendar (HTTP {(int)response.StatusCode})."
        };
        throw new FamilyApiException(response.StatusCode, message);
    }

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
public sealed record ProfileDto(Guid MemberId, string DisplayName, string Role, string? AvatarUrl);
public sealed record QuietHoursDto(bool Enabled, int? StartMinutes, int? EndMinutes, string? StartLabel, string? EndLabel);
public sealed record UpdateQuietHoursBody(bool Enabled, int? StartMinutes, int? EndMinutes);

public sealed record GoogleCalendarStatusDto(bool Connected, string? Email, string? CalendarId, DateTime? LastSyncAtUtc, bool Configured);
public sealed record GoogleCalendarAuthUrlDto(string Url);
public sealed record GoogleCalendarSyncDto(int Changed);
