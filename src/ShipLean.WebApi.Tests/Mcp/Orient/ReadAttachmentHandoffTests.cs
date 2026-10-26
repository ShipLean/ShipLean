using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Orient;

// One agent attaches research and comments; another token for the same user reads attachments, text and the full log.
[TestFixture]
public class ReadAttachmentHandoffTests : McpFixture
{
    private string _featureKey = null!;

    [OneTimeSetUp]
    public async Task SeedFeatureAsync()
    {
        var writer = await TokenAsync(TestUser.Joel, ReadWrite);
        var created = await CallToolAsync(writer, "create_feature", """
            {"project":"SHIP","title":"Handoff probe","goal":"Test read-back.","status":"Starting research."}
            """);
        created.IsToolError().ShouldBeFalse();
        _featureKey = created.Structured().GetProperty("key").GetString()!;
    }

    [Test]
    public async Task SecondToken_ShouldReadAttachmentListTextAndFullLog()
    {
        var writer = await TokenAsync(TestUser.Joel, ReadWrite);
        var reader = await TokenAsync(TestUser.Joel, ReadOnly);

        await CallToolAsync(writer, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"research-x.md","content":"# Findings\n\nQuokka latency is the bottleneck."}""");
        for (var i = 1; i <= 3; i++)
        {
            await CallToolAsync(writer, "comment", $$"""{"item":"{{_featureKey}}","text":"Session log line {{i}}."}""");
        }

        var item = await CallToolAsync(reader, "get_work_item", $$"""{"key":"{{_featureKey}}"}""");
        item.IsToolError().ShouldBeFalse();
        var attachments = item.Structured().GetProperty("attachments").EnumerateArray().ToList();
        attachments.ShouldHaveSingleItem();
        attachments[0].GetProperty("name").GetString().ShouldBe("research-x.md");
        attachments[0].GetProperty("is_markdown").GetBoolean().ShouldBeTrue();

        var read = await CallToolAsync(reader, "read_attachment", $$"""{"item":"{{_featureKey}}","name":"research-x.md"}""");
        read.IsToolError().ShouldBeFalse();
        read.Structured().GetProperty("markdown").GetString()!.ShouldContain("Quokka latency");

        var firstPage = item.Structured().GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("body").GetString()).ToList();
        firstPage.Count.ShouldBe(3);
        firstPage.ShouldBe(["Session log line 3.", "Session log line 2.", "Session log line 1."]);
        item.Structured().TryGetProperty("comments_older", out _).ShouldBeFalse();
    }
}
