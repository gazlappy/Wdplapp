using System.Security.Cryptography;
using System.Text;
using Wdpl2.Services.Web;

namespace wdpl2.Tests;

/// <summary>
/// The hash format is a contract with PHP: <c>Auth::verifyHash()</c> parses
/// exactly what <see cref="PasswordHash.Create"/> writes. These tests pin the
/// format so a change here cannot silently lock the secretary out of a deployed
/// backend.
/// </summary>
public class PasswordHashTests
{
    [Fact]
    public void Create_ProducesTheFormatThePhpSideParses()
    {
        var hash = PasswordHash.Create("correct horse battery staple");
        var parts = hash.Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 210_000, "Iteration count must not regress.");
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
    }

    [Fact]
    public void Verify_AcceptsTheCorrectPassword()
    {
        var hash = PasswordHash.Create("s3cret-pass");
        Assert.True(PasswordHash.Verify("s3cret-pass", hash));
    }

    [Fact]
    public void Verify_RejectsAWrongPassword()
    {
        var hash = PasswordHash.Create("s3cret-pass");
        Assert.False(PasswordHash.Verify("s3cret-pas", hash));
        Assert.False(PasswordHash.Verify("", hash));
    }

    [Fact]
    public void Create_SaltsEachHashSeparately()
    {
        Assert.NotEqual(PasswordHash.Create("same"), PasswordHash.Create("same"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("bcrypt$1$aaaa$bbbb")]
    [InlineData("pbkdf2-sha256$100$aaaa$bbbb")]      // iteration count below the floor
    [InlineData("pbkdf2-sha256$210000$!!!$bbbb")]    // salt is not base64
    [InlineData("pbkdf2-sha256$210000$aaaa")]        // truncated
    public void Verify_RejectsMalformedStoredValues(string stored)
    {
        Assert.False(PasswordHash.Verify("anything", stored));
    }

    /// <summary>
    /// Reproduces PHP's <c>hash_pbkdf2('sha256', ..., true)</c> independently and
    /// checks our verifier agrees, so the two implementations cannot drift apart
    /// without a test failing.
    /// </summary>
    [Fact]
    public void Verify_MatchesAnIndependentlyComputedPbkdf2()
    {
        const string password = "league-admin";
        const int iterations = 210_000;

        var salt = RandomNumberGenerator.GetBytes(16);
        var expected = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);

        var stored = $"pbkdf2-sha256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(expected)}";

        Assert.True(PasswordHash.Verify(password, stored));
        Assert.False(PasswordHash.Verify("league-admin ", stored));
    }

    /// <summary>
    /// The cross-language contract, pinned to one shared vector.
    /// </summary>
    /// <remarks>
    /// This exact string was produced by PHP's
    /// <c>hash_pbkdf2('sha256', 'wdpl-shared-vector', str_repeat("*", 16), 210000, 32, true)</c>
    /// and is asserted from both sides: here, and by
    /// <c>wdpl2.Tests/Features/WebPlatform/backend.rules.test.php</c>.
    /// If either assertion fails, the app and a deployed backend no longer agree
    /// on the admin password format and the secretary cannot sign in.
    /// </remarks>
    [Fact]
    public void Verify_AcceptsAVectorGeneratedByPhp()
    {
        const string stored =
            "pbkdf2-sha256$210000$KioqKioqKioqKioqKioqKg==$idSuNmNd2D7SOvYi/JIjFbdSi8jexZuBGR/ccUlnu2Q=";

        Assert.True(PasswordHash.Verify("wdpl-shared-vector", stored));
        Assert.False(PasswordHash.Verify("wdpl-shared-vecto", stored));
    }

    [Fact]
    public void CreatePepper_IsRandomAndLongEnough()
    {
        var a = PasswordHash.CreatePepper();
        var b = PasswordHash.CreatePepper();

        Assert.NotEqual(a, b);
        Assert.Equal(32, Convert.FromBase64String(a).Length);
    }
}
