using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>REST chat client; household membership is enforced by the API.</summary>
public sealed class ChatApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public ChatApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public Task<ChatChannelDto> GetChannelAsync(CancellationToken ct = default)
        => SendAsync<ChatChannelDto>(HttpMethod.Get, "api/chat/channel", null, ct);

    public Task<List<ChatMessageDto>> GetMessagesAsync(Guid? channelId = null, int take = 50,
        DateTime? beforeUtc = null, CancellationToken ct = default)
    {
        var query = $"?take={Math.Clamp(take, 1, 100)}";
        if (channelId is not null) query += $"&channelId={channelId:D}";
        if (beforeUtc is not null) query += $"&beforeUtc={Uri.EscapeDataString(beforeUtc.Value.ToUniversalTime().ToString("O"))}";
        return SendAsync<List<ChatMessageDto>>(HttpMethod.Get, "api/chat/messages" + query, null, ct);
    }

    public Task<ChatMessageDto> SendAsync(string body, Guid? channelId = null, CancellationToken ct = default)
        => SendAsync<ChatMessageDto>(HttpMethod.Post, "api/chat/messages",
            new StringContent(JsonSerializer.Serialize(new SendChatMessageBody(body, channelId), JsonOptions),
                Encoding.UTF8, "application/json"), ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in to use family chat.");

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
                HttpStatusCode.Forbidden => "Your account cannot access this household's chat.",
                HttpStatusCode.NotFound => "The family chat channel could not be found.",
                _ => $"FamilyOS could not complete the chat request (HTTP {(int)response.StatusCode})."
            };
            try
            {
                var payload = await response.Content.ReadAsStringAsync(timeout.Token);
                using var document = JsonDocument.Parse(payload);
                foreach (var key in new[] { "detail", "error", "title" })
                    if (document.RootElement.TryGetProperty(key, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        message = value.GetString()!;
                        break;
                    }
            }
            catch (JsonException) { }
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("The FamilyOS chat API returned an empty response.");
    }
}

public sealed record ChatChannelDto(Guid Id, string Name, string Kind);
public sealed record ChatMessageDto(Guid Id, Guid ChannelId, Guid SenderMemberId, string SenderName, string Body, DateTime SentAtUtc);
public sealed record SendChatMessageBody(string Body, Guid? ChannelId);
