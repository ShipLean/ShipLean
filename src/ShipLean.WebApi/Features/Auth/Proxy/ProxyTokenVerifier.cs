using Agoda.IoC.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ShipLean.Application.Identity;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// Token mode: a proxy's JWT becomes an identity only when its signature checks out against the issuer's keys
// (ProxyKeySource), its iss is the configured issuer, its aud contains the configured audience (when one is set), it
// has an exp that has not passed (2 minutes of skew at most), its email_verified claim (if sent) is true, and its email
// claim is a usable email. Anything else is
// no identity, logged at Warning with the reason and never the token. IdentityModel hides claim values and tokens in
// its exception messages (PII off by default), so those messages are safe to log.
[RegisterSingleton]
public sealed class ProxyTokenVerifier
{
    public const string EmailVerifiedClaim = "email_verified";

    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    // Asymmetric signatures only: the keys come from a public JWKS, so never "none" and never a shared secret.
    private static readonly string[] Algorithms =
    [
        SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.RsaSsaPssSha384, SecurityAlgorithms.RsaSsaPssSha512,
        SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384, SecurityAlgorithms.EcdsaSha512,
    ];

    private readonly ProxySignInSettings _settings;
    private readonly ProxyKeySource _keys;
    private readonly ILogger<ProxyTokenVerifier> _logger;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly TokenValidationParameters? _parameters;

    public ProxyTokenVerifier(ProxySignInSettings settings, ProxyKeySource keys, ILogger<ProxyTokenVerifier> logger)
    {
        _settings = settings;
        _keys = keys;
        _logger = logger;
        if (keys.Manager is { } manager)
        {
            _parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = settings.Audience is not null,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = ClockSkew,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = Algorithms,
                // The shared manager, never a copy of its keys in IssuerSigningKeys: then an unknown kid refreshes them.
                ConfigurationManager = manager,
            };
        }
    }

    // The identity the token proves, or null. Never throws for a bad token or unreachable keys.
    public async Task<ProxyIdentity?> VerifyAsync(string token)
    {
        if (_parameters is null || _keys.Manager is not { } manager)
        {
            return null;
        }
        // Fetched (or taken from the cache) on each request before validation, so unreachable keys are logged as that
        // and not as a signature failure. CancellationToken.None: one aborted request must not fail the fetch every
        // request shares; the client's 10-second timeout bounds it.
        var failure = await FetchFailureAsync(manager);
        if (failure is not null)
        {
            _logger.LogWarning("Proxy sign-in refused a token: the signing keys could not be fetched ({Reason}).", failure);
            return null;
        }
        var result = await _handler.ValidateTokenAsync(token, _parameters);
        if (!result.IsValid)
        {
            _logger.LogWarning(
                "Proxy sign-in refused a token: it failed verification ({ErrorType}: {Error}).",
                result.Exception?.GetType().Name ?? "unknown", result.Exception?.Message ?? "no reason given");
            return null;
        }
        if (!EmailVerifiedOrUnstated(result))
        {
            _logger.LogWarning("Proxy sign-in refused a token: email not verified ({Claim}).", EmailVerifiedClaim);
            return null;
        }
        var email = IdentityInput.Email(StringClaim(result, _settings.EmailClaim));
        if (email is null)
        {
            _logger.LogWarning("Proxy sign-in refused a token: no usable email claim ({EmailClaim}).", _settings.EmailClaim);
            return null;
        }
        return new ProxyIdentity(email, IdentityInput.Name(StringClaim(result, _settings.NameClaim)));
    }

    // Why the keys can't be had, or null. Only ProxyKeySource's own messages (host and path, the kind of failure) are
    // passed on; anything else is named by its type, so no URL query or response body reaches a log line.
    private static async Task<string?> FetchFailureAsync(ConfigurationManager<OpenIdConnectConfiguration> manager)
    {
        try
        {
            await manager.GetConfigurationAsync(CancellationToken.None);
            return null;
        }
        catch (InvalidOperationException e)
        {
            return e.InnerException is IOException or InvalidOperationException ? e.InnerException.Message : (e.InnerException ?? e).GetType().Name;
        }
    }

    // email_verified (OpenID Connect): when the provider states it, it must be true (JSON true, or the string "true"
    // in any case). Anything else stated refuses, so an unverified address can't adopt the account that owns it. A
    // token without the claim passes: Cloudflare Access, for one, never sends it.
    private static bool EmailVerifiedOrUnstated(TokenValidationResult result)
        => PayloadValue(result, EmailVerifiedClaim) is not { } value
           || value is true
           || (value is string text && string.Equals(text, "true", StringComparison.OrdinalIgnoreCase));

    // A claim that is a single string; a missing, array or object claim is null.
    private static string? StringClaim(TokenValidationResult result, string name) => PayloadValue(result, name) as string;

    // The claim as the payload has it, read from the token itself: TokenValidationResult.Claims flattens a one-element
    // array into its element, which would let ["x"] pass as "x".
    private static object? PayloadValue(TokenValidationResult result, string name)
        => result.SecurityToken is JsonWebToken token && token.TryGetPayloadValue<object>(name, out var value) ? value : null;
}
