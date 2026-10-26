using System.Net;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Gate;

// A registered tool nobody gave an [McpScope] is a bug that fails closed: it never runs, whatever the token holds.
[TestFixture]
public class McpUnscopedToolTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenToolHasNoScope_ShouldFailClosedAndNotRunIt()
    {
        var token = await TokenAsync(TestUser.Mei, ReadWritePlan);

        var reply = await CallToolAsync(token, "probe_unscoped");

        ((int)reply.Status).ShouldBeGreaterThanOrEqualTo(500);
        Log.Runs("Unscoped").ShouldBe(0);
        reply.Body.ShouldNotContain("ran", Case.Sensitive);
    }
}
