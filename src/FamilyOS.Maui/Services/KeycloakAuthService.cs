using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Storage;

namespace FamilyOS.Maui.Services;

public interface IAuthSessionService
{
    Task<string> SignInAsync(CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task SignOutAsync();
}

/// <summary>
/// Native-system-browser Keycloak Authorization Code + PKCE client.
/// This is a public native client: no client secret is stored or sent.
/// </summary>
public sealed class KeycloakAuthService : IAuthSessionService
{
    public const string Authority = "https://auth.18.226.226.118.sslip.io/realms/familyos";
    public const string ClientId = "familyos-mobile";
    public const string RedirectUri = "familyos://auth";

    private const string AccessTokenKey = "familyos.auth.access_token";
    private const string RefreshTokenKey = "familyos.auth.refresh_token";
    private const string ExpiresAtKey = "familyos.auth.expires_at_utc";

    private static string AuthorizationEndpoint => $"{Authority}/protocol/openid-connect/auth";
    private static string TokenEndpoint => $"{Authority}/protocol/openid-connect/token";

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public KeycloakAuthService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> SignInAsync(CancellationToken cancellationToken = default)
    {
        var verifier = Pkce.CreateVerifier();
        var state = Pkce.CreateState();
        var challenge = Pkce.CreateChallenge(verifier);

        var query = string.Join("&",
        [
            $"client_id={Uri.EscapeDataString(ClientId)}",
            "response_type=code",
            $"redirect_uri={Uri.EscapeDataString(RedirectUri)}",
            $"scope={Uri.EscapeDataString("openid profile email offline_access")}",
            $"state={Uri.EscapeDataString(state)}",
            $"code_challenge={Uri.EscapeDataString(challenge)}",
            "code_challenge_method=S256"
        ]);

        var authResult = await WebAuthenticator.Default.AuthenticateAsync(
            new Uri($"{AuthorizationEndpoint}?{query}"),
            new Uri(RedirectUri),
            cancellationToken);

        if (!authResult.Properties.TryGetValue("state", out var returnedState)
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(state),
                System.Text.Encoding.UTF8.GetBytes(returnedState)))
        {
            throw new InvalidOperationException("The identity provider returned an invalid sign-in state.");
        }

        if (!authResult.Properties.TryGetValue("code", out var code)
            || string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("The identity provider did not return an authorization code.");
        }

        var tokens = await RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = verifier
        }, cancellationToken);

        await SaveTokensAsync(tokens, fallbackRefreshToken: null);
        return tokens.AccessToken;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
        var expiresAt = await SecureStorage.Default.GetAsync(ExpiresAtKey);

        if (!string.IsNullOrWhiteSpace(accessToken)
            && DateTimeOffset.TryParse(expiresAt, out var expiry)
            && expiry > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return accessToken;
        }

        return await RefreshAccessTokenAsync(cancellationToken);
    }

    public async Task SignOutAsync()
    {
        await SecureStorage.Default.RemoveAsync(AccessTokenKey);
        await SecureStorage.Default.RemoveAsync(RefreshTokenKey);
        await SecureStorage.Default.RemoveAsync(ExpiresAtKey);
    }

    private async Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            // Another request may have refreshed while this request waited.
            var currentAccess = await SecureStorage.Default.GetAsync(AccessTokenKey);
            var currentExpiry = await SecureStorage.Default.GetAsync(ExpiresAtKey);
            if (!string.IsNullOrWhiteSpace(currentAccess)
                && DateTimeOffset.TryParse(currentExpiry, out var expiry)
                && expiry > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return currentAccess;
            }

            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            TokenResponse tokens;
            try
            {
                tokens = await RequestTokensAsync(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = ClientId,
                    ["refresh_token"] = refreshToken
                }, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                await SignOutAsync();
                return null;
            }

            await SaveTokensAsync(tokens, refreshToken);
            return tokens.AccessToken;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<TokenResponse> RequestTokensAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(form),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Keycloak token request failed with HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken: cancellationToken);

        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
            throw new InvalidOperationException("Keycloak returned an incomplete token response.");

        return tokens;
    }

    private static async Task SaveTokensAsync(TokenResponse tokens, string? fallbackRefreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, tokens.AccessToken);

        var refresh = string.IsNullOrWhiteSpace(tokens.RefreshToken)
            ? fallbackRefreshToken
            : tokens.RefreshToken;
        if (!string.IsNullOrWhiteSpace(refresh))
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refresh);

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(tokens.ExpiresIn, 60));
        await SecureStorage.Default.SetAsync(ExpiresAtKey, expiresAt.ToString("O"));
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
