using System.Security.Cryptography;
using System.Text;

namespace Wdpl2.Services.Web;

/// <summary>
/// Produces the admin password hash stored in the backend's <c>config.php</c>.
/// </summary>
/// <remarks>
/// Format: <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;</c>,
/// verified by <c>Auth::verifyHash()</c> in the PHP core.
/// <para>
/// PBKDF2-SHA256 is used because both .NET and PHP implement it natively. PHP's
/// own <c>password_hash()</c> defaults to bcrypt, which would mean adding a
/// third-party package to this app just to generate one string.
/// </para>
/// <para>The plaintext password is never stored or transmitted anywhere but the
/// Authorization header of a live HTTPS request.</para>
/// </remarks>
public static class PasswordHash
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string Create(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password must not be empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Verifies locally, so the app can check a stored hash without a round trip.</summary>
    public static bool Verify(string password, string stored)
    {
        var parts = (stored ?? "").Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256") return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations < 1000) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expected.Length == 0) return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password ?? ""), salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>Random value for the backend's rate-limit pepper.</summary>
    public static string CreatePepper() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
