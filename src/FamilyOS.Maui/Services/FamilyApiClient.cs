using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    public async Task<List<FamilyInviteDto>> GetInvitesAsync(CancellationToken ct = default)
        => await SendAsync<List<FamilyInviteDto>>(HttpMethod.Get, "api/invites", null, ct);

    public async Task<FamilyInviteDto> CreateInviteAsync(string role, string? emailHint, CancellationToken ct = default)
        => await SendAsync<FamilyInviteDto>(HttpMethod.Post, "api/invites",
            JsonContent.Create(new { role, emailHint, validDays = 14 }, options: JsonOptions), ct);

    public async Task RevokeInviteAsync(Guid inviteId, CancellationToken ct = default)
        => await SendNoContentAsync(HttpMethod.Post, $"api/invites/{inviteId:D}/revoke", null, ct);

    public async Task<FamilyMemberDto> ChangeMemberRoleAsync(Guid memberId, string role, CancellationToken ct = default)
        => await SendAsync<FamilyMemberDto>(HttpMethod.Post, $"api/family/members/{memberId:D}/change-role",
            JsonContent.Create(new { role }, options: JsonOptions), ct);

    public async Task<FamilyMemberDto> DeactivateMemberAsync(Guid memberId, CancellationToken ct = default)
        => await SendAsync<FamilyMemberDto>(HttpMethod.Post, $"api/family/members/{memberId:D}/deactivate", null, ct);

    public Task<List<CheckInDto>> GetCheckInsAsync(bool includeCompleted = true, CancellationToken ct = default)
        => SendAsync<List<CheckInDto>>(HttpMethod.Get, $"api/checkins?includeCompleted={includeCompleted.ToString().ToLowerInvariant()}", null, ct);

    public Task<CheckInDto> CreateCheckInAsync(Guid targetMemberId, DateTimeOffset dueAtUtc, CancellationToken ct = default)
        => SendAsync<CheckInDto>(HttpMethod.Post, "api/checkins",
            JsonContent.Create(new { targetMemberId, dueAtUtc }, options: JsonOptions), ct);

    public Task RespondToCheckInAsync(Guid id, string? message = null, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Post, $"api/checkins/{id:D}/respond",
            JsonContent.Create(new { message }, options: JsonOptions), ct);

    public Task CancelCheckInAsync(Guid id, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Post, $"api/checkins/{id:D}/cancel", null, ct);

    public Task<SosResultDto> SendSosAsync(string? message, double? latitude, double? longitude, CancellationToken ct = default)
        => SendAsync<SosResultDto>(HttpMethod.Post, "api/location/sos", JsonContent.Create(new
        {
            Message = message,
            IncludeLocation = latitude.HasValue && longitude.HasValue,
            Latitude = latitude,
            Longitude = longitude
        }), ct);

    public Task<LocationConsentDto> GetLocationConsentAsync(CancellationToken ct = default)
        => SendAsync<LocationConsentDto>(HttpMethod.Get, "api/location/consent", null, ct);

    public Task<LocationConsentDto> SetLocationSharingAsync(bool enabled, CancellationToken ct = default)
        => SendAsync<LocationConsentDto>(HttpMethod.Put, "api/location/consent",
            JsonContent.Create(new { enabled }, options: JsonOptions), ct);

    public Task ShareLocationAsync(double latitude, double longitude, double accuracyMeters, DateTime measuredAtUtc, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Post, "api/location/current",
            JsonContent.Create(new { latitude, longitude, accuracyMeters, measuredAtUtc }, options: JsonOptions), ct);

    public Task<List<FamilyLocationDto>> GetFamilyLocationsAsync(CancellationToken ct = default)
        => SendAsync<List<FamilyLocationDto>>(HttpMethod.Get, "api/location/family", null, ct);

    public Task<List<LocationViewerDto>> GetLocationViewersAsync(CancellationToken ct = default)
        => SendAsync<List<LocationViewerDto>>(HttpMethod.Get, "api/location/viewers", null, ct);

    public Task SetLocationViewerAsync(Guid viewerMemberId, bool enabled, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Put, $"api/location/viewers/{viewerMemberId:D}",
            JsonContent.Create(new { enabled }, options: JsonOptions), ct);

    public Task<LocationHistoryConsentDto> GetLocationHistoryConsentAsync(CancellationToken ct = default)
        => SendAsync<LocationHistoryConsentDto>(HttpMethod.Get, "api/location/history/consent", null, ct);

    public Task<LocationHistoryConsentDto> SetLocationHistoryConsentAsync(bool enabled, CancellationToken ct = default)
        => SendAsync<LocationHistoryConsentDto>(HttpMethod.Put, "api/location/history/consent",
            JsonContent.Create(new { enabled }, options: JsonOptions), ct);

    public Task<List<LocationHistoryPointDto>> GetLocationHistoryAsync(Guid memberId,
        DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null, CancellationToken ct = default)
    {
        var path = $"api/location/history/{memberId:D}";
        var query = new List<string>();
        if (fromUtc.HasValue) query.Add($"fromUtc={Uri.EscapeDataString(fromUtc.Value.ToString("O"))}");
        if (toUtc.HasValue) query.Add($"toUtc={Uri.EscapeDataString(toUtc.Value.ToString("O"))}");
        if (query.Count > 0) path += "?" + string.Join("&", query);
        return SendAsync<List<LocationHistoryPointDto>>(HttpMethod.Get, path, null, ct);
    }

    public Task<List<LocationTripDto>> GetLocationTripsAsync(Guid memberId,
        DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null, CancellationToken ct = default)
    {
        var path = $"api/location/trips/{memberId:D}";
        var query = new List<string>();
        if (fromUtc.HasValue) query.Add($"fromUtc={Uri.EscapeDataString(fromUtc.Value.ToString("O"))}");
        if (toUtc.HasValue) query.Add($"toUtc={Uri.EscapeDataString(toUtc.Value.ToString("O"))}");
        if (query.Count > 0) path += "?" + string.Join("&", query);
        return SendAsync<List<LocationTripDto>>(HttpMethod.Get, path, null, ct);
    }

    public Task ClearLocationHistoryAsync(CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, "api/location/history", null, ct);

    public Task<List<SavedPlaceDto>> GetSavedPlacesAsync(CancellationToken ct = default)
        => SendAsync<List<SavedPlaceDto>>(HttpMethod.Get, "api/location/places", null, ct);

    public Task<SavedPlaceDto> CreateSavedPlaceAsync(string name, double latitude, double longitude,
        double radiusMeters, bool alertOnArrival, bool alertOnDeparture, CancellationToken ct = default)
        => SendAsync<SavedPlaceDto>(HttpMethod.Post, "api/location/places",
            JsonContent.Create(new { name, latitude, longitude, radiusMeters, alertOnArrival, alertOnDeparture }, options: JsonOptions), ct);

    public Task UpdateSavedPlaceAsync(Guid placeId, string name, double latitude, double longitude,
        double radiusMeters, bool alertOnArrival, bool alertOnDeparture, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Put, $"api/location/places/{placeId:D}",
            JsonContent.Create(new { name, latitude, longitude, radiusMeters, alertOnArrival, alertOnDeparture }, options: JsonOptions), ct);

    public Task DeleteSavedPlaceAsync(Guid placeId, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/location/places/{placeId:D}", null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to manage family invitations.");
        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new FamilyApiException(response.StatusCode, response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session has expired. Sign in again.",
                HttpStatusCode.Forbidden => "Only household adults can manage invitations.",
                _ => $"FamilyOS could not complete that action (HTTP {(int)response.StatusCode})."
            });
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("The FamilyOS API returned an empty response.");
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to manage family invitations.");
        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new FamilyApiException(response.StatusCode, $"FamilyOS could not complete that request (HTTP {(int)response.StatusCode}).");
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

public sealed record FamilyInviteDto(Guid Id, string Code, string Role, string? EmailHint, DateTime ExpiresAtUtc, bool IsOpen, DateTime? AcceptedAtUtc);
public sealed record FamilyDto(Guid Id, string Name, string? TimeZone, string? Currency, List<FamilyMemberDto> Members, Guid CurrentMemberId);
public sealed record FamilyMemberDto(Guid Id, Guid UserId, string DisplayName, string Role, bool IsActive);
public sealed record SosResultDto(int AlertsSent, bool LocationIncluded);\npublic sealed record LocationConsentDto(bool SharingEnabled, DateTime? ConsentUpdatedAtUtc);
public sealed record FamilyLocationDto(Guid MemberId, string DisplayName, double? Latitude, double? Longitude, double? AccuracyMeters, DateTime? UpdatedAtUtc);
public sealed record LocationViewerDto(Guid MemberId, string DisplayName, bool CanView);

public sealed record SavedPlaceDto(Guid Id, string Name, double Latitude, double Longitude, double RadiusMeters, bool AlertOnArrival, bool AlertOnDeparture, bool CanEdit);
public sealed record LocationHistoryConsentDto(bool HistoryEnabled);
public sealed record LocationHistoryPointDto(double Latitude, double Longitude, double AccuracyMeters, DateTime RecordedAtUtc);
public sealed record LocationTripDto(DateTime StartedAtUtc, DateTime EndedAtUtc, double StartLatitude, double StartLongitude, double EndLatitude, double EndLongitude, double DistanceMeters, double DurationMinutes);

public sealed record CheckInDto(Guid Id, Guid RequesterMemberId, string RequesterName, Guid TargetMemberId, string TargetName, DateTime CreatedAtUtc, DateTime DueAtUtc, string Status, DateTime? RespondedAtUtc, string? ResponseMessage, bool CanRespond);
