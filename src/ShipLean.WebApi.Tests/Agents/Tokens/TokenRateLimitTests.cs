using System.Net;
using ShipLean.WebApi.Features.Agents.Tokens;

namespace ShipLean.WebApi.Tests.Agents.Tokens;

[TestFixture]
public class TokenRateLimitTests : TokenFixture
{
    [Test]
    public async Task Token_WhenOverLimit_ShouldAnswer429()
    {
        for (var i = 0; i < TokenServices.RequestsPerMinutePerIp; i++)
        {
            var (response, _) = await TokenAsync(new() { ["grant_type"] = "password" });
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var (limited, _) = await TokenAsync(new() { ["grant_type"] = "password" });

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
