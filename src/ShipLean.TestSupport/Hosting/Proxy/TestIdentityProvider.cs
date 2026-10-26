using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ShipLean.TestSupport.Hosting.Proxy;

// A stand-in identity provider on Kestrel at http://127.0.0.1:<free port>: one RSA key (kid test-key-1), its JWKS at
// /jwks, a discovery document at /.well-known/openid-configuration, and /signed-out for the sign-out redirect. Mint
// signs tokens as the proxy would forward them. Start it before the ShipLean host (ProxyApiFixture.UseIdentityProvider)
// and dispose it at teardown.
public sealed class TestIdentityProvider : IAsyncDisposable
{
    public const string KeyId = "test-key-1";

    public const string DefaultTokenHeader = "X-Test-Proxy-Token";

    // The issuer of minted tokens when JWKS_URL is set (no discovery): a name, never fetched.
    public const string Issuer = "https://idp.shiplean.test";

    private readonly WebApplication _app;
    private readonly RSA _key;

    private TestIdentityProvider(WebApplication app, RSA key, string baseUrl)
    {
        _app = app;
        _key = key;
        DiscoveryIssuer = baseUrl;
    }

    // Its own base URL, the issuer its discovery document names.
    public string DiscoveryIssuer { get; }

    public string JwksUrl => $"{DiscoveryIssuer}/jwks";

    public string SignedOutUrl => $"{DiscoveryIssuer}/signed-out";

    // A URL that answers 302 to JwksUrl, for a test that key fetches follow no redirects.
    public string RedirectingJwksUrl => $"{DiscoveryIssuer}/jwks-redirect";

    // When set, the discovery document names this issuer instead of DiscoveryIssuer (a document for another issuer).
    // Set it before the host fetches the document: the host caches it.
    public string? DiscoveryIssuerOverride { get; set; }

    // When set, the discovery document's jwks_uri is this instead of JwksUrl. Set it before the host fetches the document.
    public string? DiscoveryJwksUriOverride { get; set; }

    public static async Task<TestIdentityProvider> StartAsync()
    {
        var key = RSA.Create(2048);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var jwks = Jwks(key);
        string? baseUrl = null;
        TestIdentityProvider? self = null;
        app.MapGet("/jwks", () => Results.Content(jwks, "application/json"));
        app.MapGet("/jwks-redirect", () => Results.Redirect("/jwks"));
        app.MapGet("/.well-known/openid-configuration", () => Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = self?.DiscoveryIssuerOverride ?? baseUrl!,
            ["jwks_uri"] = self?.DiscoveryJwksUriOverride ?? $"{baseUrl}/jwks",
            ["id_token_signing_alg_values_supported"] = new[] { SecurityAlgorithms.RsaSha256 },
            ["subject_types_supported"] = new[] { "public" },
            ["response_types_supported"] = new[] { "code" },
        }));
        app.MapGet("/signed-out", () => Results.Content(
            "<!doctype html><html><head><title>Signed out</title></head><body><p data-testid=\"idp-signed-out\">You are signed out.</p></body></html>",
            "text/html"));
        await app.StartAsync();
        baseUrl = $"http://127.0.0.1:{new Uri(app.Urls.First()).Port}";
        self = new TestIdentityProvider(app, key, baseUrl);
        return self;
    }

    // A signed RS256 token. A null email leaves the email claim out; a null name leaves the name claim out. Times come
    // from TimeProvider.System; an expired token gets nbf and iat before its exp.
    public string Mint(string? email, string? name = null, TokenOptions? options = null)
    {
        options ??= new TokenOptions();
        var now = TimeProvider.System.GetUtcNow();
        var expires = now + options.ExpiresIn;
        var issuedAt = expires > now ? now : expires - TimeSpan.FromMinutes(10);
        var claims = new Dictionary<string, object> { ["sub"] = email ?? "no-email" };
        if (email != null)
        {
            claims[options.EmailClaim] = email;
        }
        if (name != null)
        {
            claims[options.NameClaim] = name;
        }
        foreach (var (claim, value) in options.ExtraClaims ?? new Dictionary<string, object>())
        {
            claims[claim] = value;
        }
        var signingKey = new RsaSecurityKey(options.SigningKey ?? _key) { KeyId = options.Kid ?? KeyId };
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer ?? Issuer,
            Audience = options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    // Token mode with the JWKS URL set (no discovery): tokens minted with the default issuer pass.
    public IReadOnlyDictionary<string, string?> Settings(string? audience = null, string tokenHeader = DefaultTokenHeader)
        => TokenSettings(tokenHeader, Issuer, JwksUrl, audience);

    // Token mode by discovery: no JWKS URL, the issuer is DiscoveryIssuer. Mint with Issuer = DiscoveryIssuer.
    public IReadOnlyDictionary<string, string?> DiscoverySettings(string? audience = null, string tokenHeader = DefaultTokenHeader)
        => TokenSettings(tokenHeader, DiscoveryIssuer, null, audience);

    // Plain-header mode (no provider needed): the email, and optionally the name, from these request headers.
    public static IReadOnlyDictionary<string, string?> HeaderSettings(string emailHeader = "Remote-Email", string? nameHeader = "Remote-Name")
    {
        var settings = new Dictionary<string, string?> { ["SHIPLEAN_AUTH_PROXY_EMAIL_HEADER"] = emailHeader };
        if (nameHeader != null)
        {
            settings["SHIPLEAN_AUTH_PROXY_NAME_HEADER"] = nameHeader;
        }
        return settings;
    }

    // The spec's literal setting names (SHIP-1), so the test infrastructure doesn't depend on the settings class.
    private static IReadOnlyDictionary<string, string?> TokenSettings(string tokenHeader, string issuer, string? jwksUrl, string? audience)
    {
        var settings = new Dictionary<string, string?>
        {
            ["SHIPLEAN_AUTH_PROXY_TOKEN_HEADER"] = tokenHeader,
            ["SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER"] = issuer,
        };
        if (jwksUrl != null)
        {
            settings["SHIPLEAN_AUTH_PROXY_TOKEN_JWKS_URL"] = jwksUrl;
        }
        if (audience != null)
        {
            settings["SHIPLEAN_AUTH_PROXY_TOKEN_AUDIENCE"] = audience;
        }
        return settings;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        _key.Dispose();
    }

    // The public half only.
    private static string Jwks(RSA key)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = SecurityAlgorithms.RsaSha256,
                    kid = KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent),
                },
            },
        });
    }
}
