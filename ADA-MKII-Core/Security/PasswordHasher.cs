using System.Globalization;
using System.Security.Cryptography;

namespace ADA_MKII_Core.Security;

/// <summary>
/// PBKDF2 password hashing. Lives in Core because two very different processes
/// must agree on the format: ADA-MKII-DataManager writes hashes when an operator
/// creates an account, and ADA-MKII-Server reads them at login.
///
/// The stored format carries its own parameters - "pbkdf2-sha256$iterations$salt$hash" -
/// so the iteration count can be raised later without invalidating existing
/// passwords: an old hash still verifies against the parameters it was made with.
/// </summary>
public static class PasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>
    /// OWASP's current floor for PBKDF2-HMAC-SHA256. Raise it over time; existing
    /// hashes keep working because each records the count it was created with.
    /// </summary>
    private const int DefaultIterations = 600_000;

    /// <summary>
    /// A hash of a value nobody knows, for verifying against when the username is
    /// unknown. Without this, login returns faster for a non-existent user than a
    /// real one, which turns the endpoint into a username oracle. Computed once:
    /// the point is to spend the same work, not to be secret.
    /// </summary>
    public static string DummyHash { get; } = Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, DefaultIterations);

        return string.Join('$',
            Algorithm,
            DefaultIterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Verifies a password. Returns false rather than throwing on a malformed
    /// stored value: a corrupt row must not become a way to bypass the check.
    /// </summary>
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm)
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations, expected.Length);

        // Fixed-time comparison: a short-circuiting equality check leaks how much
        // of the hash matched.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
}
