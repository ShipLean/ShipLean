using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// SHIP-63 J1: token sign-in end to end. A real signed token from TestIdentityProvider, the real reader fetching its
// JWKS over HTTP, the real handler, the real cookie.
[TestFixture]
public class ProxyTokenJourneyTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    [Test]
    public async Task TokenSignIn_WhenValidTokenAndNoCookie_ShouldCreateUserAndSetCookie()
    {
        var email = NewEmail();
        using var client = ProxyClient().WithToken(Header, Provider!.Mint(email, "Ada Lovelace"));

        using var response = await client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await MeAsync(response)).Email.ShouldBe(email);
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith(AuthServices.CookieName + "=", StringComparison.Ordinal) && !c.StartsWith(AuthServices.CookieName + "=;", StringComparison.Ordinal));
        await using var db = await OpenDatabaseAsync();
        (await db.QueryAsync<string>("SELECT name FROM app_user WHERE lower(email) = lower(@email)", new { email })).ToList().ShouldBe(["Ada Lovelace"]);
    }

    [Test]
    public async Task TokenSignIn_WhenSameTokenWithCookie_ShouldKeepTheUser()
    {
        var email = NewEmail();
        using var client = ProxyClient().WithToken(Header, Provider!.Mint(email, "Grace Hopper"));
        var first = await client.GetJsonAsync<Me>("/api/me");

        using var again = await client.GetAsync("/api/me");

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MeAsync(again)).Id.ShouldBe(first.Id);
        again.Headers.Contains("Set-Cookie").ShouldBeFalse();
        await using var db = await OpenDatabaseAsync();
        (await db.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email })).ShouldBe(1);
    }

    [Test]
    public async Task Instance_WhenProxyMode_ShouldReportProxy()
    {
        using var client = ProxyClient();

        var instance = await client.GetJsonAsync<System.Text.Json.Nodes.JsonNode>("/api/auth/instance");

        instance["signIn"]!.GetValue<string>().ShouldBe("proxy");
    }

    private static string Header => ShipLean.TestSupport.Hosting.Proxy.TestIdentityProvider.DefaultTokenHeader;

    private static string NewEmail() => $"journey-{Guid.NewGuid():N}@shiplean.test";

    private static async Task<Me> MeAsync(HttpResponseMessage response)
        => JsonSerializer.Deserialize<Me>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions.Web)!;

    private sealed record Me(Guid Id, string Email, string Name);
}
