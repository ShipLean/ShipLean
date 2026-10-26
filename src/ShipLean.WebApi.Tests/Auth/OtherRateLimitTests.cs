using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Tests.Auth;

// The sign-in sentence belongs to the sign-in limiter alone; any other limited endpoint answers the generic message.
[TestFixture]
public class OtherRateLimitTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddControllers().AddApplicationPart(typeof(RateLimitProbeController).Assembly);
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(RateLimitProbeController.Policy, _ => RateLimitPartition.GetFixedWindowLimiter(
            "probe", _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) })));
    };

    [Test]
    public async Task OtherPolicy_WhenRateLimited_ShouldAnswerTheGenericMessage()
    {
        using var client = AuthClient.Create(Host);
        (await client.GetAsync("/api/test-limit/once")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var limited = await client.GetAsync("/api/test-limit/once");

        var error = await ApiClient.ReadApiErrorAsync(limited, HttpStatusCode.TooManyRequests);
        error.Code.ShouldBe(ApiErrorCodes.RateLimited);
        error.Error.ShouldBe(AuthServices.GenericRateLimitMessage);
        error.Error.ShouldNotBe(SignInService.Refusal);
    }
}
