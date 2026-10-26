using Dapper;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Shapes;

// The plan's tags go through the ordinary tag store unchanged, ':', '.' and '/' included and case kept.
[TestFixture]
public class TagStorageTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task Tags_WithColonDotAndSlash_ShouldBeStored()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Tagged by a plan").InsertAsync(connection);
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
        var planned = new PlanTaskTags(WaveLabel.Parse("0.1"), "F", "strong", 55, "feat/2-feature-editor", Orphan: true);
        string[] tags = ["agent", .. PlanTags.Format(planned)];
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "refine_work_item", $$"""{"item":"{{key}}","tags":{{System.Text.Json.JsonSerializer.Serialize(tags)}}}""");

        reply.IsToolError().ShouldBeFalse();
        var stored = await connection.QuerySingleAsync<string[]>("SELECT tags FROM work_item WHERE id = @story", new { story });
        stored.ShouldBe(tags);
        PlanTags.Parse(stored).ShouldBe(planned);
    }
}
