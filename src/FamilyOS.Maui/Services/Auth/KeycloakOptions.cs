namespace FamilyOS.Maui.Services.Auth;

/// <summary>
/// Keycloak OIDC settings for PKCE login. Values typically come from configuration
/// or build-time defaults for local/dev environments.
/// </summary>
public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "familyos-mobile";
    public string RedirectUri { get; set; } = "familyos://auth/callback";
    public string Scope { get; set; } = "openid profile email";
}
