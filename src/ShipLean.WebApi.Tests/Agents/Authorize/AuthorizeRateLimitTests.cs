using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Agents.Authorize;

namespace ShipLean.WebApi.Tests.Agents.Authorize;

[TestFixture]
public sealed class AuthorizeRateLimitTests : AuthorizeFixtureBase
{
    [Test]
    public async Task Authorize_WhenOverLimit_ShouldAnswer429()
    {
        using var browser = SignedOutBrowser();
        for (var i = 0; i < AuthorizeServices.RequestsPerMinutePerIp; i++)
        {
            using var allowed = await browser.GetAsync("/oauth/authorize");
            allowed.StatusCode.ShouldBe(HttpStatusCode.Found);
        }

        using var limited = await browser.GetAsync("/oauth/authorize");

        (await ApiClient.ReadApiErrorAsync(limited, HttpStatusCode.TooManyRequests)).Code.ShouldBe(ApiErrorCodes.RateLimited);
    }
}
