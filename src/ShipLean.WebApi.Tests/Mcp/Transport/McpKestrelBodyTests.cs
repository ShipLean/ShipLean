using System.Net;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// Kestrel refuses a body over 30 MB on its own. A file at the default attachment limit encodes past that, so /mcp has to
// raise the request's limit or the call never reaches the gate.
[TestFixture]
public class McpKestrelBodyTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenBodyOverKestrelsDefaultAndUnderTheCap_ShouldRun()
    {
        McpBodyLimit.BytesFor(AttachmentLimits.DefaultMaxBytes).ShouldBeGreaterThan(McpBodyLimit.KestrelDefaultMaxBodyBytes + 1);
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var padding = new string('x', checked((int)McpBodyLimit.KestrelDefaultMaxBodyBytes + 1));

        var reply = await CallToolAsync(token, "probe_whoami", $$"""{"pad":"{{padding}}"}""");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        Log.Runs("Whoami").ShouldBe(1);
    }
}
