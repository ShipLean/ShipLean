using System.Net;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// The gate reads the whole body before the SDK does: capped at 1 MB, one message per request, and the SDK still gets it.
[TestFixture]
public class McpBodyTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenBodyOver1MB_ShouldAnswer413()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var padding = new string('x', 1024 * 1024 + 1);

        var reply = await PostAsync(token, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"probe_whoami\",\"arguments\":{\"pad\":\"" + padding + "\"}}}");

        reply.Status.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        Log.Runs("Whoami").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenBodyJustUnder1MB_ShouldRun()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var padding = new string('x', 1024 * 1024 - 512);

        var reply = await CallToolAsync(token, "probe_whoami", $$"""{"pad":"{{padding}}"}""");

        reply.Status.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Mcp_WhenBatch_ShouldAnswer400()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await PostAsync(token, """[{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"probe_whoami","arguments":{}}}]""");

        reply.Status.ShouldBe(HttpStatusCode.BadRequest);
        reply.Body.ShouldContain("batches are not supported");
        Log.Runs("Whoami").ShouldBe(0);
    }
}
