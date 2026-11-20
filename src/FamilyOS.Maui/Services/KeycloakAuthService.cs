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
    private readonly StartupDiagnosticsService _diagnostics;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public KeycloakAuthService(HttpClient httpClient, StartupDiagnosticsService diagnostics)
    {
        _httpClient = httpClient;
        _diagnostics = diagnostics;
    }

    public async Task<string> SignInAsync(CancellationToken cancellationToken = default)
    {
        _diagnostics.RecordCheckpoint("AUTH sign-in started");
        try
        {
        var verifier = Pkce.CreateVerifier();
        var state = Pkce.CreateState();
        var challenge = Pkce.CreateChallenge(verifier);
        _diagnostics.RecordCheckpoint("AUTH PKCE verifier/state/challenge generated");

        var query = string.Join("&", new[]
        {
            $"client_id={Uri.EscapeDataString(ClientId)}",
            "response_type=code",
            $"redirect_uri={Uri.EscapeDataString(RedirectUri)}",
            $"scope={Uri.EscapeDataString("openid profile email offline_access")}",
            $"state={Uri.EscapeDataString(state)}",
            $"code_challenge={Uri.EscapeDataString(challenge)}",
            "code_challenge_method=S256"
        });

        _diagnostics.RecordCheckpoint("AUTH launching system browser; redirect scheme=familyos");
        var authResult = await WebAuthenticator.Default.AuthenticateAsync(
            new Uri($"{AuthorizationEndpoint}?{query}"),
            new Uri(RedirectUri),
            cancellationToken);
        _diagnostics.RecordCheckpoint($"AUTH browser returned; property_count={authResult.Properties.Count}");

        if (!authResult.Properties.TryGetValue("state", out var returnedState)
            || returnedState.Length != state.Length
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

        _diagnostics.RecordCheckpoint("AUTH redirect state validated; authorization code present");
        var tokens = await RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = verifier
        }, cancellationToken);

        await SaveTokensAsync(tokens, fallbackRefreshToken: null);
        _diagnostics.RecordCheckpoint("AUTH sign-in completed; tokens stored in secure storage (values omitted)");
        return tokens.AccessToken;
        }
        catch (OperationCanceledException)
        {
            _diagnostics.RecordCheckpoint("AUTH sign-in cancelled by user or platform");
            throw;
        }
        catch (Exception ex)
        {
            _diagnostics.RecordCheckpoint($"AUTH sign-in failed; exception={ex.GetType().Name}; http_status={(ex is HttpRequestException http && http.StatusCode.HasValue ? (int)http.StatusCode.Value : 0)}");
            throw;
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        _diagnostics.RecordCheckpoint("AUTH access-token lookup started");
        var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
        var expiresAt = await SecureStorage.Default.GetAsync(ExpiresAtKey);

        if (!string.IsNullOrWhiteSpace(accessToken)
            && DateTimeOffset.TryParse(expiresAt, out var expiry)
            && expiry > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            _diagnostics.RecordCheckpoint("AUTH cached access token is valid");
            return accessToken;
        }

        _diagnostics.RecordCheckpoint($"AUTH cached access token unavailable_or_expired; token_present={!string.IsNullOrWhiteSpace(accessToken)}");
        return await RefreshAccessTokenAsync(cancellationToken);
    }

    public Task SignOutAsync()
    {
        _diagnostics.RecordCheckpoint("AUTH sign-out started; clearing secure token keys");
        // ISecureStorage in .NET 11 exposes synchronous Remove(string), not RemoveAsync.
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        _diagnostics.RecordCheckpoint("AUTH sign-out completed; local token keys cleared");
        return Task.CompletedTask;
    }

    private async Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        _diagnostics.RecordCheckpoint("AUTH refresh requested");
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
            {
                _diagnostics.RecordCheckpoint("AUTH refresh skipped; no refresh token in secure storage");
                return null;
            }
            _diagnostics.RecordCheckpoint("AUTH refresh token present; requesting new access token (value omitted)");

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
                _diagnostics.RecordCheckpoint($"AUTH refresh rejected; http_status={(int?)ex.StatusCode}");
                await SignOutAsync();
                return null;
            }

            await SaveTokensAsync(tokens, refreshToken);
            _diagnostics.RecordCheckpoint("AUTH refresh succeeded; token values omitted");
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
        var grantType = form.TryGetValue("grant_type", out var grant) ? grant : "unknown";
        _diagnostics.RecordCheckpoint($"AUTH token endpoint request started; grant_type={grantType}");
        using var response = await _httpClient.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(form),
            cancellationToken);

        _diagnostics.RecordCheckpoint($"AUTH token endpoint response; http_status={(int)response.StatusCode}; success={response.IsSuccessStatusCode}");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Keycloak token request failed with HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken: cancellationToken);

        if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
        {
            _diagnostics.RecordCheckpoint("AUTH token response invalid; access token missing (response body omitted)");
            throw new InvalidOperationException("Keycloak returned an incomplete token response.");
        }

        _diagnostics.RecordCheckpoint($"AUTH token response parsed; expires_in_seconds={tokens.ExpiresIn}; refresh_token_present={!string.IsNullOrWhiteSpace(tokens.RefreshToken)}");
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
