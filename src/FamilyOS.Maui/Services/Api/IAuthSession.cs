namespace FamilyOS.Maui.Services.Api;

/// <summary>
/// Holds the active API session. Production uses a Keycloak JWT;
/// DEBUG builds may use X-Dev-User for local API work without OIDC.
/// </summary>
public interface IAuthSession
{
    string? AccessToken { get; }
    string? DevUser { get; }
    string BaseUrl { get; }
    bool IsAuthenticated { get; }

    void SetAccessToken(string? token);
    void SetDevUser(string? externalId);
    void SetBaseUrl(string baseUrl);
    void Clear();
}
