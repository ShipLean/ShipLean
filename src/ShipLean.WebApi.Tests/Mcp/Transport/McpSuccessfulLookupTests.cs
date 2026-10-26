using System.Net;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// Only failures count against an address: a busy agent with a good token is never limited by the lookup limit.
[TestFixture]
public class McpSuccessfulLookupTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenManyGoodLookups_ShouldNeverTripTheFailureLimit()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        for (var i = 0; i < 40; i++)
        {
            (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK, $"call {i + 1}");
        }
    }
}
