using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Transport;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// Where agents are told to upload large arguments instead of sending them inline.
[TestFixture]
public class PayloadGuidanceTests : McpFixture
{
    private static readonly string[] PayloadTools = ["attach_markdown", "break_down", "capture_plan", "capture_work", "create_feature", "refine_work_item"];

    protected override bool WithProbeTools => false;

    [Test]
    public async Task ListTools_WhenToolTakesPayload_ShouldMentionCreatePayloadUpload()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var listed = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        var tools = listed.Json().GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
        var missing = tools
            .Where(tool => PayloadTools.Contains(tool.GetProperty("name").GetString()!))
            .Where(tool => tool.GetProperty("description").GetString()?.Contains("create_payload_upload") != true)
            .Select(tool => tool.GetProperty("name").GetString())
            .ToList();
        missing.ShouldBeEmpty();
        tools.Count(tool => PayloadTools.Contains(tool.GetProperty("name").GetString()!)).ShouldBe(PayloadTools.Length);
    }

    [Test]
    public void ServerInstructions_WhenConnecting_ShouldMentionPayloadUploads()
    {
        McpServerServices.Instructions.ShouldContain("create_payload_upload");
        McpServerServices.Instructions.ShouldContain("about 20 KB");
    }
}
