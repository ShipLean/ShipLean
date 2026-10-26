using System.Net;
using System.Security.Cryptography;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Hosting.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// SHIP-63 J1: a token the reader refuses leaves the caller anonymous: 401, and no user row.
[TestFixture]
public class ProxyTokenRefusalJourneyTests : ProxyApiFixture
{
    private const string Audience = "shiplean-journey";

    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => Provider!.Settings(audience: Audience);

    [Test]
    public async Task TokenSignIn_WhenGoodToken_ShouldSignIn()
    {
        var email = NewEmail();
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint(email, "Good Token", Good));

        using var response = await client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [TestCase("wrong-key")]
    [TestCase("wrong-issuer")]
    [TestCase("expired")]
    [TestCase("wrong-audience")]
    [TestCase("no-email")]
    public async Task TokenSignIn_WhenBadToken_ShouldAnswerUnauthorizedAndCreateNoUser(string defect)
    {
        var email = NewEmail();
        using var forgery = RSA.Create(2048);
        var token = defect switch
        {
            "wrong-key" => Provider!.Mint(email, "Bad", Good with { SigningKey = forgery }),
            "wrong-issuer" => Provider!.Mint(email, "Bad", Good with { Issuer = "https://other.shiplean.test" }),
            "expired" => Provider!.Mint(email, "Bad", Good with { ExpiresIn = TimeSpan.FromMinutes(-10) }),
            "wrong-audience" => Provider!.Mint(email, "Bad", Good with { Audience = "someone-else" }),
            "no-email" => Provider!.Mint(null, "Bad", Good),
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, token);

        using var response = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        await using var db = await OpenDatabaseAsync();
        (await db.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email) OR name = 'Bad'", new { email })).ShouldBe(0);
    }

    // The account-takeover case: a token that names an existing user's email but says the address is not verified.
    [Test]
    public async Task TokenSignIn_WhenEmailNotVerifiedForExistingUser_ShouldAnswerUnauthorized()
    {
        var unverified = Good with { ExtraClaims = new Dictionary<string, object> { ["email_verified"] = false } };
        using var client = ProxyClient().WithToken(TestIdentityProvider.DefaultTokenHeader, Provider!.Mint(TestUser.Mei.Email!, "Mei", unverified));

        using var response = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    private static TokenOptions Good => new() { Audience = Audience };

    private static string NewEmail() => $"refused-{Guid.NewGuid():N}@shiplean.test";
}
