using System.Security.Cryptography;
using System.Text;
using ShipLean.Application.Features.Agents.Stores;

namespace ShipLean.Application.Features.DeviceSessions;

// The device-session secret and sign-in code, shared by every IDeviceSessions. A secret is "shl_dev_" and 43 base62
// characters (256 bits); a code is 32 random bytes, base64url. Only their SHA-256 is stored. The PKCE pieces are S256
// (RFC 7636). Hashing and comparing reuse StoreSecrets (same assembly).
public static class DeviceSecret
{
    public const string Prefix = "shl_dev_";
    public const int RandomLength = 43;
    public const int TokenPrefixLength = 16;
    public const int ChallengeLength = 43;

    private const string Base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    public static string New() => Prefix + RandomNumberGenerator.GetString(Base62, RandomLength);

    public static string NewCode() => StoreSecrets.New();

    public static byte[] Hash(string? secret) => StoreSecrets.Hash(secret);

    // What a list shows of the secret: "shl_dev_" and the next 8 characters.
    public static string TokenPrefix(string secret) => secret[..TokenPrefixLength];

    // Whether a bearer value has the shape of a device secret (the selector's test; the store decides if it is real).
    public static bool HasShape(string? value)
        => value is { Length: var length } && length == Prefix.Length + RandomLength
           && value.StartsWith(Prefix, StringComparison.Ordinal) && value.AsSpan(Prefix.Length).IndexOfAnyExcept(Base62) < 0;

    // Constant time; both sides are SHA-256 digests.
    public static bool Same(byte[] given, byte[] expected) => StoreSecrets.Same(given, expected);

    // S256: base64url(SHA-256(ASCII(verifier))).
    public static string ChallengeFor(string verifier) => StoreSecrets.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    // Constant-time S256 check of a verifier against the stored challenge.
    public static bool VerifierMatches(string? verifier, string challenge) => StoreSecrets.Same(ChallengeFor(verifier ?? string.Empty), challenge);

    // 43 base64url characters: the only challenge shape S256 produces.
    public static bool IsChallenge(string? value)
        => value is { Length: ChallengeLength } && value.AsSpan().IndexOfAnyExcept(Base64UrlAlphabet) < 0;
}
