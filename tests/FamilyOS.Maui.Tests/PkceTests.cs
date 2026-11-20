using System.Security.Cryptography;
using System.Text;
using FamilyOS.Maui.Services;
using Xunit;

namespace FamilyOS.Maui.Tests;

public class PkceTests
{
    [Fact]
    public void Verifier_is_url_safe_and_meets_rfc7636_length()
    {
        var verifier = Pkce.CreateVerifier();

        Assert.InRange(verifier.Length, 43, 128);
        Assert.All(verifier, character =>
            Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
    }

    [Fact]
    public void Challenge_is_sha256_base64url_of_verifier()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var expected = Convert.ToBase64String(
                SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        Assert.Equal(expected, Pkce.CreateChallenge(verifier));
    }

    [Fact]
    public void State_values_are_fresh_and_url_safe()
    {
        var first = Pkce.CreateState();
        var second = Pkce.CreateState();

        Assert.NotEqual(first, second);
        Assert.Equal(43, first.Length);
        Assert.All(first, character =>
            Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
    }
}
