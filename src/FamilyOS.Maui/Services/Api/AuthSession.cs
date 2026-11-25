namespace FamilyOS.Maui.Services.Api;

/// <summary>
/// Holds the active API session and persists it in Preferences so cold start
/// can restore a DEBUG/dev session or a previously stored JWT.
/// Production Keycloak PKCE (#32) will write tokens here after callback.
/// </summary>
public sealed class AuthSession : IAuthSession
{
    private const string KeyBaseUrl = "familyos.api.baseUrl";
    private const string KeyToken = "familyos.api.accessToken";
    private const string KeyDevUser = "familyos.api.devUser";

#if DEBUG
    private string _baseUrl = "http://10.0.2.2:5080"; // Android emulator → host loopback
#else
    private string _baseUrl = "https://api.18.226.226.118.sslip.io";
#endif

    public AuthSession()
    {
        var savedBase = Preferences.Default.Get(KeyBaseUrl, string.Empty);
        if (!string.IsNullOrWhiteSpace(savedBase))
            _baseUrl = savedBase.Trim().TrimEnd('/');

        AccessToken = NullIfEmpty(Preferences.Default.Get(KeyToken, string.Empty));
#if DEBUG
        DevUser = NullIfEmpty(Preferences.Default.Get(KeyDevUser, string.Empty));
#else
        // Never restore X-Dev-User outside DEBUG builds.
        DevUser = null;
        Preferences.Default.Remove(KeyDevUser);
#endif
    }

    public string? AccessToken { get; private set; }
    public string? DevUser { get; private set; }
    public string BaseUrl => _baseUrl;
    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) || !string.IsNullOrWhiteSpace(DevUser);

    public void SetAccessToken(string? token)
    {
        AccessToken = NullIfEmpty(token);
        if (AccessToken is null)
            Preferences.Default.Remove(KeyToken);
        else
            Preferences.Default.Set(KeyToken, AccessToken);
    }

    public void SetDevUser(string? externalId)
    {
#if DEBUG
        DevUser = NullIfEmpty(externalId);
        if (DevUser is null)
            Preferences.Default.Remove(KeyDevUser);
        else
            Preferences.Default.Set(KeyDevUser, DevUser);
#else
        DevUser = null;
        Preferences.Default.Remove(KeyDevUser);
#endif
    }

    public void SetBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return;
        _baseUrl = baseUrl.Trim().TrimEnd('/');
        Preferences.Default.Set(KeyBaseUrl, _baseUrl);
    }

    public void Clear()
    {
        AccessToken = null;
        DevUser = null;
        Preferences.Default.Remove(KeyToken);
        Preferences.Default.Remove(KeyDevUser);
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
