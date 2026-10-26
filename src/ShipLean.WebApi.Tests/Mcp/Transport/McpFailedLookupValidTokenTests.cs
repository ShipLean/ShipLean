using System.Net;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// A good token gets in from an address that has used up its failures.
[TestFixture]
public class McpFailedLookupValidTokenTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenAddressIsBlockedAndTokenIsValid_ShouldRunTheTool()
    {
        var good = await TokenAsync(TestUser.Joel, ReadOnly);
        for (var i = 0; i < 25; i++)
        {
            await CallToolAsync($"wrong-{i}", "probe_whoami");
        }

        var reply = await CallToolAsync(good, "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        Log.Runs("Whoami").ShouldBe(1);
    }
}
