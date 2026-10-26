using System.Security.Cryptography;
using System.Text;

namespace ShipLean.Application.Features.Agents.Stores;

// Codes and tokens are 32 random bytes, base64url; only their SHA-256 is stored.
internal static class StoreSecrets
{
    public static string New() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string? secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret ?? string.Empty));

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // Constant time. Lengths are public (both sides are SHA-256 digests or fixed-shape strings).
    public static bool Same(byte[] given, byte[] expected) => CryptographicOperations.FixedTimeEquals(given, expected);

    public static bool Same(string? given, string expected)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given ?? string.Empty), Encoding.UTF8.GetBytes(expected));
}
