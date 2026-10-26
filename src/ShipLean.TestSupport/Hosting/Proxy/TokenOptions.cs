using System.Security.Cryptography;

namespace ShipLean.TestSupport.Hosting.Proxy;

// How TestIdentityProvider.Mint shapes one token. Every null means the provider's own value.
public sealed record TokenOptions
{
    // The iss claim (default: TestIdentityProvider.Issuer; DiscoveryIssuer for a discovery-mode test).
    public string? Issuer { get; init; }

    // The aud claim; left out when null.
    public string? Audience { get; init; }

    // exp relative to now. Negative for an expired token (nbf and iat are then set before exp). Keep it past the
    // validator's clock skew (2 minutes) for a token that must be refused.
    public TimeSpan ExpiresIn { get; init; } = TimeSpan.FromMinutes(10);

    // The kid header (default: TestIdentityProvider.KeyId).
    public string? Kid { get; init; }

    // Signs with this key instead of the provider's: a forgery, still named by Kid.
    public RSA? SigningKey { get; init; }

    // The claim names the email and name go in, for non-default SHIPLEAN_AUTH_PROXY_EMAIL_CLAIM / _NAME_CLAIM.
    public string EmailClaim { get; init; } = "email";

    public string NameClaim { get; init; } = "name";

    // More claims, added last, so they can replace sub, the email or the name claim. They can't replace iss, exp, nbf
    // or iat: the token handler writes those from the properties above.
    public IReadOnlyDictionary<string, object>? ExtraClaims { get; init; }
}
