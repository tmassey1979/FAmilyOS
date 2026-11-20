using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyOS.Maui.Services;

/// <summary>Household creation and invitation acceptance using existing API contracts.</summary>
public sealed class OnboardingApiClient
{
    private static readonly Uri ApiBaseUri = new("https://api.18.226.226.118.sslip.io/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly IAuthSessionService _auth;

    public OnboardingApiClient(HttpClient http, IAuthSessionService auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<FamilyDto> CreateFamilyAsync(
        string name, string? displayName, string? email, string timeZone, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            name,
            timeZone,
            currency = "USD",
            ownerDisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            ownerEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim()
        }, JsonOptions);
        return await SendAsync<FamilyDto>(HttpMethod.Post, "api/family",
            new StringContent(body, Encoding.UTF8, "application/json"), authenticated: true, ct);
    }

    public Task<InvitePreviewDto> PreviewInviteAsync(string code, CancellationToken ct = default)
        => SendAsync<InvitePreviewDto>(HttpMethod.Get,
            $"api/invites/{Uri.EscapeDataString(NormalizeCode(code))}", null, authenticated: false, ct);

    public async Task<FamilyMemberDto> AcceptInviteAsync(
        string code, string? displayName, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            displayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim()
        }, JsonOptions);
        return await SendAsync<FamilyMemberDto>(HttpMethod.Post,
            $"api/invites/{Uri.EscapeDataString(NormalizeCode(code))}/accept",
            new StringContent(body, Encoding.UTF8, "application/json"), authenticated: true, ct);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content, bool authenticated, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(ApiBaseUri, path)) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");

        if (authenticated)
        {
            var token = await _auth.GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token))
                throw new FamilyApiException(HttpStatusCode.Unauthorized, "Sign in before continuing.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your sign-in has expired. Sign in again.",
                HttpStatusCode.Forbidden => "This account cannot complete that onboarding action.",
                HttpStatusCode.NotFound => "That invitation could not be found.",
                HttpStatusCode.Conflict => "This account already belongs to a family.",
                _ => $"FamilyOS could not complete onboarding (HTTP {(int)response.StatusCode})."
            };
            throw new FamilyApiException(response.StatusCode, message);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, timeout.Token);
        return result ?? throw new InvalidOperationException("The FamilyOS API returned an empty onboarding response.");
    }

    private static string NormalizeCode(string code)
        => (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
}

public sealed record InvitePreviewDto(string Code, string FamilyName, string Role, DateTime ExpiresAtUtc, bool IsOpen);
