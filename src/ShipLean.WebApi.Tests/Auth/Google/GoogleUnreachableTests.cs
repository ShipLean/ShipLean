using System.Net;
using ShipLean.WebApi.Features.Auth.Google;

namespace ShipLean.WebApi.Tests.Auth.Google;

// Google's configuration is fetched on the first start. The stand-in for Google is stopped before that, so the fetch is
// refused, as it is when ShipLean has no route to Google.
[TestFixture]
public class GoogleUnreachableTests : GoogleSignInFixture
{
    [Test]
    public async Task Start_WhenGoogleCannotBeReached_ShouldReturnToSignInKeepingTheReturnUrl()
    {
        await Provider.DisposeAsync();
        using var browser = Browser();

        using var response = await browser.GetAsync($"/api/auth/google/start?returnUrl={Uri.EscapeDataString("/p/PAY")}");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldBe($"/login?state={GoogleSignInEvents.FailedState}&returnUrl=%2Fp%2FPAY");
        await ShouldHaveNoSessionAsync(browser);
    }

    [TestCase("https://evil.example/p/PAY")]
    [TestCase("//evil.example/p/PAY")]
    public async Task Start_WhenGoogleCannotBeReachedAndReturnUrlIsNotLocal_ShouldReturnToSignInWithTheRoot(string returnUrl)
    {
        await Provider.DisposeAsync();
        using var browser = Browser();

        using var response = await browser.GetAsync($"/api/auth/google/start?returnUrl={Uri.EscapeDataString(returnUrl)}");

        response.Headers.Location!.ToString().ShouldBe($"/login?state={GoogleSignInEvents.FailedState}&returnUrl=%2F");
    }
}
