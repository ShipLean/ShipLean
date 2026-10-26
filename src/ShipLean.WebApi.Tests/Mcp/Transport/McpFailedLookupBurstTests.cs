using System.Net;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// The count is atomic: a burst of failures at once is let through no further than the limit.
[TestFixture]
public class McpFailedLookupBurstTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenFailedLookupsArriveTogether_ShouldLetThroughNoMoreThanTheLimit()
    {
        var replies = await Task.WhenAll(Enumerable.Range(0, 60).Select(i => CallToolAsync($"burst-{i}", "probe_whoami")));

        replies.Count(reply => reply.Status == HttpStatusCode.Unauthorized).ShouldBe(20);
        replies.Count(reply => reply.Status == HttpStatusCode.TooManyRequests).ShouldBe(40);
    }
}
