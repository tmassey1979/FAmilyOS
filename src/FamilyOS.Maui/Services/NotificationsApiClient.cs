using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Authenticated inbox operations using the existing notifications API.</summary>
public sealed class NotificationsApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public NotificationsApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public Task<List<FamilyNotificationDto>> GetMineAsync(CancellationToken ct = default)
        => SendAsync<List<FamilyNotificationDto>>(HttpMethod.Get, "api/notifications", null, ct);

    public Task<FamilyNotificationDto> MarkReadAsync(Guid id, CancellationToken ct = default)
        => SendAsync<FamilyNotificationDto>(HttpMethod.Post, $"api/notifications/{id:D}/read", null, ct);

    public Task<MarkNotificationsReadDto> MarkAllReadAsync(CancellationToken ct = default)
        => SendAsync<MarkNotificationsReadDto>(HttpMethod.Post, "api/notifications/read-all", null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to view notifications.");

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
                HttpStatusCode.Forbidden => "You do not have access to these notifications.",
                _ => $"FamilyOS could not load notifications (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token);
        return result ?? throw new InvalidOperationException("The FamilyOS API returned an empty notification response.");
    }
}

public sealed record FamilyNotificationDto(
    Guid Id, string Type, string Title, string Body, Guid? RelatedEntityId,
    string? RelatedEntityType, bool IsRead, DateTime? ReadAtUtc, DateTime CreatedAtUtc);

public sealed record MarkNotificationsReadDto(int Marked);
