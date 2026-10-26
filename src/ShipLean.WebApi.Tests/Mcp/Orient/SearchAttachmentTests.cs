using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Orient;

// search over Markdown attachment text, on Postgres (the in-memory store keeps no search text).
[TestFixture]
public class SearchAttachmentTests : McpFixture
{
    private string _attachmentOnlyKey = string.Empty;
    private string _titleKey = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        var attachmentOnly = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Quiet feature").InsertAsync(connection);
        var titled = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Quokkaline rollout").InsertAsync(connection);
        _attachmentOnlyKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @attachmentOnly", new { attachmentOnly });
        _titleKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @titled", new { titled });

        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var attached = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_attachmentOnlyKey}}","name":"quokkaline-spec.md","content":"The quokkaline handshake needs two steps."}""");
        attached.IsToolError().ShouldBeFalse(attached.ResultText());

        // Both items are real entities, so the full sync keeps them and indexes the attachment's text.
        using var reindexed = await Api.ActingAs(TestUser.Joel).PostJsonAsync("api/admin/search/reindex?full=true");
        reindexed.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Search_WhenWordOnlyInMarkdownAttachment_ShouldReturnItemNamingFile()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "search", """{"project":"SHIP","words":"handshake"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var matches = reply.Structured().GetProperty("matches");
        matches.GetArrayLength().ShouldBe(1);
        matches[0].GetProperty("key").GetString().ShouldBe(_attachmentOnlyKey);
        matches[0].GetProperty("attachment").GetString().ShouldBe("quokkaline-spec.md");
    }

    [Test]
    public async Task Search_WhenWordInTitleAndOtherItemsAttachment_ShouldRankTitleFirst()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "search", """{"project":"SHIP","words":"quokkaline"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var matches = reply.Structured().GetProperty("matches");
        matches.GetArrayLength().ShouldBe(2);
        matches[0].GetProperty("key").GetString().ShouldBe(_titleKey);
        matches[1].GetProperty("key").GetString().ShouldBe(_attachmentOnlyKey);
    }
}
