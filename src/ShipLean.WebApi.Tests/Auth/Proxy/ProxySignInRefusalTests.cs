using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The sign-in page states (K9) with no session: neither cookie nor identity is refused, /login itself never redirects
// again, and a /login load that does carry an identity is signed in and sent on to returnUrl by the cookie handler.
[TestFixture]
public class ProxySignInRefusalTests : ProxyApiFixture
{
    [Test]
    public async Task SignIn_WhenNeitherCookieNorIdentity_ShouldAnswerUnauthorized()
    {
        using var client = ProxyClient();

        using var api = await client.GetAsync("/api/me");
        using var page = await client.GetAsync("/");
        using var login = await client.GetAsync("/login?returnUrl=%2F");

        (await ApiClient.ReadApiErrorAsync(api, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
        page.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        page.Headers.Location!.PathAndQuery.ShouldBe("/login?returnUrl=%2F");
        // This host serves no SPA bundle, so /login is the page fallback's 404 here; what counts is that it is neither
        // refused nor redirected.
        login.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        ((int)login.StatusCode).ShouldNotBeInRange(300, 399);
        login.Headers.Location.ShouldBeNull();
    }

    [Test]
    public async Task SignIn_WhenLoginPageLoadedWithIdentity_ShouldRedirectToReturnUrl()
    {
        using var client = ProxyClient().WithIdentity(TestUser.Priya.Email!);

        using var login = await client.GetAsync("/login?returnUrl=%2Fp%2FPAY");

        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        new Uri(new Uri(Host.BaseUrl), login.Headers.Location!).PathAndQuery.ShouldBe("/p/PAY");
    }

    // The cookie handler follows only a host-relative returnUrl, so a sign-in page load never sends anyone off-site.
    [TestCase("%2F%2Fevil.example%2Fsteal")]
    [TestCase("https%3A%2F%2Fevil.example%2Fsteal")]
    [TestCase("%2F%5Cevil.example%2Fsteal")]
    public async Task SignIn_WhenLoginReturnUrlIsOffSite_ShouldNotRedirectOffSite(string returnUrl)
    {
        using var client = ProxyClient().WithIdentity(TestUser.Priya.Email!);

        using var login = await client.GetAsync($"/login?returnUrl={returnUrl}");

        var location = login.Headers.Location;
        if (location != null)
        {
            new Uri(new Uri(Host.BaseUrl), location).Authority.ShouldBe(new Uri(Host.BaseUrl).Authority);
        }
        login.Headers.Contains("Set-Cookie").ShouldBeTrue();
    }
}
