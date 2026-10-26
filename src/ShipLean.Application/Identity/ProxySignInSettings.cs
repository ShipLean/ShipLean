using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Identity;

// Sign-in through an identity proxy, from the SHIPLEAN_AUTH_PROXY_* settings (docs/auth.md). There is no picker: proxy
// sign-in is on when one of the two identity settings is present, the token header (a signed JWT, recommended) or the
// email header (plain-header mode). With neither, Mode is Password and every other setting is ignored. Parse throws an
// InvalidOperationException naming the setting at fault, which fails startup (AuthServices validates it on start).
public sealed record ProxySignInSettings(
    ProxySignInMode Mode,
    string? TokenHeader,
    string? Issuer,
    string? JwksUrl,
    string? Audience,
    string EmailClaim,
    string NameClaim,
    string? EmailHeader,
    string? NameHeader,
    string? SignOutUrl)
{
    public const string TokenHeaderKey = "SHIPLEAN_AUTH_PROXY_TOKEN_HEADER";
    public const string IssuerKey = "SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER";
    public const string JwksUrlKey = "SHIPLEAN_AUTH_PROXY_TOKEN_JWKS_URL";
    public const string AudienceKey = "SHIPLEAN_AUTH_PROXY_TOKEN_AUDIENCE";
    public const string EmailClaimKey = "SHIPLEAN_AUTH_PROXY_EMAIL_CLAIM";
    public const string NameClaimKey = "SHIPLEAN_AUTH_PROXY_NAME_CLAIM";
    public const string EmailHeaderKey = "SHIPLEAN_AUTH_PROXY_EMAIL_HEADER";
    public const string NameHeaderKey = "SHIPLEAN_AUTH_PROXY_NAME_HEADER";
    public const string SignOutUrlKey = "SHIPLEAN_AUTH_PROXY_SIGN_OUT_URL";

    public const string DefaultEmailClaim = "email";
    public const string DefaultNameClaim = "name";

    private static readonly string[] LoopbackHosts = ["127.0.0.1", "[::1]", "localhost"];

    public static ProxySignInSettings PasswordOnly { get; } =
        new(ProxySignInMode.Password, null, null, null, null, DefaultEmailClaim, DefaultNameClaim, null, null, null);

    public bool IsProxy => Mode != ProxySignInMode.Password;

    public static ProxySignInSettings Parse(IConfiguration configuration)
    {
        var tokenHeader = Read(configuration, TokenHeaderKey);
        var emailHeader = Read(configuration, EmailHeaderKey);
        if (tokenHeader is not null && emailHeader is not null)
        {
            throw new InvalidOperationException(
                $"{TokenHeaderKey} and {EmailHeaderKey} are both set: say who the user is one way, so set only one of them.");
        }
        if (tokenHeader is null && emailHeader is null)
        {
            return PasswordOnly;
        }
        var signOutUrl = Read(configuration, SignOutUrlKey);
        if (signOutUrl is not null && !IsSignOutUrl(signOutUrl))
        {
            throw new InvalidOperationException(
                $"{SignOutUrlKey} is neither a path starting with a single / nor an absolute http(s) URL without credentials.");
        }
        if (tokenHeader is not null)
        {
            var issuer = Read(configuration, IssuerKey)
                         ?? throw new InvalidOperationException($"{IssuerKey} is required when {TokenHeaderKey} is set.");
            RequireKeyUrl(IssuerKey, issuer);
            var jwksUrl = Read(configuration, JwksUrlKey);
            if (jwksUrl is not null)
            {
                RequireKeyUrl(JwksUrlKey, jwksUrl);
            }
            return new ProxySignInSettings(
                ProxySignInMode.Token, tokenHeader, issuer, jwksUrl, Read(configuration, AudienceKey),
                Read(configuration, EmailClaimKey) ?? DefaultEmailClaim, Read(configuration, NameClaimKey) ?? DefaultNameClaim,
                null, null, signOutUrl);
        }
        return new ProxySignInSettings(
            ProxySignInMode.Header, null, null, null, null, DefaultEmailClaim, DefaultNameClaim,
            emailHeader, Read(configuration, NameHeaderKey), signOutUrl);
    }

    // The issuer and the JWKS URL (configured or discovered): absolute https, or http to the loopback host only (a local
    // identity provider), and never with user:password@ in it, which the startup log would otherwise print.
    public static bool IsKeyUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && uri.UserInfo.Length == 0
           && (uri.Scheme == Uri.UriSchemeHttps
               || (uri.Scheme == Uri.UriSchemeHttp && LoopbackHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)));

    private static void RequireKeyUrl(string key, string value)
    {
        if (!IsKeyUrl(value))
        {
            throw new InvalidOperationException(
                $"{key} is not an absolute https URL without credentials (plain http is allowed only to 127.0.0.1, [::1] or localhost).");
        }
    }

    // A path on this host (/oauth2/sign_out; never //host, nor a backslash, which browsers read as / so /\host is //host),
    // or an absolute http(s) URL without credentials. The messages never echo the value, which could carry credentials.
    private static bool IsSignOutUrl(string value)
        => value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\')
           || Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.UserInfo.Length == 0
              && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string? Read(IConfiguration configuration, string key)
        => configuration[key]?.Trim() is { Length: > 0 } value ? value : null;
}
