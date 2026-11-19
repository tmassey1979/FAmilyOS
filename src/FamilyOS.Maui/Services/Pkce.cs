using System.Security.Cryptography;
using System.Text;

namespace FamilyOS.Maui.Services;

/// <summary>
/// RFC 7636 S256 primitives. Kept independent of MAUI so the security-critical
/// encoding rules can be exercised by the host test runner.
/// </summary>
public static class Pkce
{
    public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string CreateChallenge(string verifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifier);
        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(digest);
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
