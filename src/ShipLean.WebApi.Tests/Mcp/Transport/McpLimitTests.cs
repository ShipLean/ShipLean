using System.Net;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// Writing tool calls are limited per user, 120 a minute; reads, previews and other users are not touched.
[TestFixture]
public class McpLimitTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenWritesOverLimit_ShouldAnswer429()
    {
        var token = await TokenAsync(TestUser.Mei, ReadWritePlan);
        var other = await TokenAsync(TestUser.Priya, ReadWritePlan);

        for (var i = 0; i < 120; i++)
        {
            (await CallToolAsync(token, "probe_write")).Status.ShouldBe(HttpStatusCode.OK, $"write {i + 1}");
        }
        var limited = await CallToolAsync(token, "probe_write");

        limited.Status.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Json().GetProperty("code").GetString().ShouldBe("rate_limited");
        Log.Runs("Write").ShouldBe(120);
        (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK);
        (await CallToolAsync(token, "probe_plan")).Status.ShouldBe(HttpStatusCode.OK);
        (await CallToolAsync(other, "probe_write")).Status.ShouldBe(HttpStatusCode.OK);
    }
}
