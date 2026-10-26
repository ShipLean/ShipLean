using System.Net;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// A rate-limited sign-in is refused with the one sentence every other sign-in failure gets.
[TestFixture]
public class LoginRateLimitTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    public async Task Login_WhenRateLimited_ShouldAnswerTheSignInSentence()
    {
        using var client = AuthClient.Create(Host);
        for (var i = 0; i < 30; i++)
        {
            (await client.LoginAsync($"limit-{i}@example.test", "wrong-password-1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var limited = await client.LoginAsync("limit-30@example.test", "wrong-password-1");

        var error = await ApiClient.ReadApiErrorAsync(limited, HttpStatusCode.TooManyRequests);
        error.Code.ShouldBe(ApiErrorCodes.RateLimited);
        error.Error.ShouldBe(SignInService.Refusal);
    }
}
