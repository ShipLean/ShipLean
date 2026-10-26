using System.Net;
using System.Text.Json;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// A shared machine: the cookie is Mei's but the proxy now says Priya, so the request acts as Priya and her cookie
// replaces Mei's.
[TestFixture]
public class ProxySignInSwitchUserTests : ProxyApiFixture
{
    [Test]
    public async Task SignIn_WhenCookieForMeiAndIdentityForPriya_ShouldActAsPriya()
    {
        using var client = ProxyClient().WithIdentity(TestUser.Mei.Email!);
        await client.GetJsonAsync<Me>("/api/me");
        var meiCookie = client.Cookie(AuthServices.CookieName)!.Value;
        client.WithIdentity(TestUser.Priya.Email!);

        using var response = await client.GetAsync("/api/me");
        client.WithoutIdentity();
        var afterwards = await client.GetJsonAsync<Me>("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonSerializer.Deserialize<Me>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions.Web)!.Id.ShouldBe(TestUser.Priya.Id!.Value);
        response.Headers.GetValues("Set-Cookie").Last().ShouldStartWith(AuthServices.CookieName + "=");
        client.Cookie(AuthServices.CookieName)!.Value.ShouldNotBe(meiCookie);
        afterwards.Id.ShouldBe(TestUser.Priya.Id.Value);
    }

    // The /api/me fields these tests read (MeDto's enums are strings on the wire).
    private sealed record Me(Guid Id, string Email, string Name, bool HasProjects);
}
