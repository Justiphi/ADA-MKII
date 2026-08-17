using System.Security.Cryptography;
using System.Text;

namespace ADA_MKII_Core.Security;

/// <summary>
/// Generation and hashing of device bearer tokens. Both the server's auth handler
/// and the store must agree on the hash, so the algorithm lives here rather than
/// in either of them.
/// </summary>
public static class DeviceTokens
{
    private const int TokenBytes = 32;

    /// <summary>Creates a new URL-safe random token. Shown to the user once; only its hash is stored.</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>Hashes a token for storage and lookup. SHA-256, lowercase hex.</summary>
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
