using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ShipLean.TestSupport.Hosting.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The in-test identity provider itself, validated with IdentityModel and its own JWKS: no ShipLean host.
[TestFixture]
public class TestIdentityProviderTests
{
    private const string Audience = "shiplean-test";

    private TestIdentityProvider _provider = null!;
    private HttpClient _http = null!;

    [OneTimeSetUp]
    public async Task StartProviderAsync()
    {
        _provider = await TestIdentityProvider.StartAsync();
        _http = new HttpClient();
    }

    [OneTimeTearDown]
    public async Task StopProviderAsync()
    {
        _http?.Dispose();
        if (_provider != null)
        {
            await _provider.DisposeAsync();
        }
    }

    [Test]
    public async Task Mint_WhenValidatedAgainstJwks_ShouldPass()
    {
        var token = _provider.Mint("mei@example.test", "Mei Tanaka", new TokenOptions { Audience = Audience });

        var result = await ValidateAsync(token);

        result.IsValid.ShouldBeTrue(result.Exception?.Message);
        result.Claims["email"].ShouldBe("mei@example.test");
        result.Claims["name"].ShouldBe("Mei Tanaka");
        ((JsonWebToken)result.SecurityToken).Kid.ShouldBe(TestIdentityProvider.KeyId);
    }

    [Test]
    public async Task Mint_WhenSignedByForeignKey_ShouldFailValidation()
    {
        using var foreignKey = RSA.Create(2048);
        var token = _provider.Mint("mei@example.test", options: new TokenOptions { Audience = Audience, SigningKey = foreignKey });

        var result = await ValidateAsync(token);

        result.IsValid.ShouldBeFalse();
        result.Exception.ShouldBeAssignableTo<SecurityTokenInvalidSignatureException>();
    }

    [Test]
    public async Task Mint_WhenExpired_ShouldFailLifetimeValidation()
    {
        var token = _provider.Mint("mei@example.test", options: new TokenOptions { Audience = Audience, ExpiresIn = TimeSpan.FromMinutes(-10) });

        var result = await ValidateAsync(token);

        result.IsValid.ShouldBeFalse();
        result.Exception.ShouldBeOfType<SecurityTokenExpiredException>();
        var jwt = new JsonWebToken(token);
        jwt.IssuedAt.ShouldBeLessThan(jwt.ValidTo);
        jwt.ValidFrom.ShouldBeLessThan(jwt.ValidTo);
    }

    [Test]
    public void Mint_WhenEmailNull_ShouldOmitTheClaim()
    {
        var token = new JsonWebToken(_provider.Mint(null, "Mei Tanaka"));

        token.TryGetPayloadValue<string>("email", out _).ShouldBeFalse();
        token.GetPayloadValue<string>("name").ShouldBe("Mei Tanaka");
    }

    [Test]
    public async Task Discovery_WhenFetched_ShouldPointAtJwks()
    {
        var configuration = await OpenIdConnectConfigurationRetriever.GetAsync(
            $"{_provider.DiscoveryIssuer}/.well-known/openid-configuration",
            new HttpDocumentRetriever(_http) { RequireHttps = false },
            CancellationToken.None);

        configuration.Issuer.ShouldBe(_provider.DiscoveryIssuer);
        configuration.JwksUri.ShouldBe(_provider.JwksUrl);
        configuration.SigningKeys.Select(key => key.KeyId).ShouldBe([TestIdentityProvider.KeyId]);
    }

    private async Task<TokenValidationResult> ValidateAsync(string token)
    {
        var jwks = new JsonWebKeySet(await _http.GetStringAsync(_provider.JwksUrl));
        return await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = TestIdentityProvider.Issuer,
            ValidAudience = Audience,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ClockSkew = TimeSpan.FromMinutes(2),
        });
    }
}
