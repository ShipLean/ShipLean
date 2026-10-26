using System.Net;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// Twenty failed token lookups a minute per network, then 429: a flood of bad tokens is slowed, but a good token is never
// limited, so nobody sharing an address can lock real agents out.
[TestFixture]
public class McpFailedLookupLimitTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenFailedLookupsOverLimit_ShouldAnswer429()
    {
        for (var i = 0; i < 20; i++)
        {
            (await CallToolAsync($"wrong-{i}", "probe_whoami")).Status.ShouldBe(HttpStatusCode.Unauthorized, $"attempt {i + 1}");
        }

        var limited = await CallToolAsync("wrong-21", "probe_whoami");

        limited.Status.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.ShouldNotBeNull();
    }
}
