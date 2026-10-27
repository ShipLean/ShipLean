using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// The gate reads the whole body before the SDK does: capped at one attachment of the instance's file limit in base64,
// one message per request, and the SDK still gets it. The file limit here is small so the cap is a few hundred KB.
[TestFixture]
public class McpBodyTests : McpFixture
{
    private const int FileBytes = 32 * 1024;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton(new AttachmentLimits(FileBytes));
    };

    [Test]
    public async Task Mcp_WhenBodyOverTheCap_ShouldAnswer413()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var padding = new string('x', checked((int)McpBodyLimit.BytesFor(FileBytes) + 1));

        var reply = await PostAsync(token, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"probe_whoami\",\"arguments\":{\"pad\":\"" + padding + "\"}}}");

        reply.Status.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        reply.Body.ShouldContain(McpBodyLimit.TooLarge(McpBodyLimit.BytesFor(FileBytes)));
        Log.Runs("Whoami").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenBodyJustUnderTheCap_ShouldRun()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var padding = new string('x', checked((int)McpBodyLimit.BytesFor(FileBytes) - 512));

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
