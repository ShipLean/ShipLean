using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Guide;

[TestFixture]
public class ResearchIdeaPromptTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task ResearchIdeaPrompt_WithDescription_ShouldNameWorkGuideAndShipLeanFlow()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"prompts/get","params":{"name":"research_idea","arguments":{"description":"Whether we should split search indexes."}}}""");

        var text = reply.Json().GetProperty("result").GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;
        text.ShouldContain("planning_guide");
        text.ShouldContain("part work");
        text.ShouldContain("attach_markdown");
        text.ShouldContain("read_attachment");
        text.ShouldContain("split search indexes");
    }
}
