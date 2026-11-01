using ShipLean.AcceptanceTests.Harness;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Insights;

// Joel has the current sprint's Retro tab open; an agent logs a retrospective over MCP and the point appears without a reload.
[TestFixture]
public class RetroLiveUpdateTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string PointText = "Pairing on the lock hand-over saved a morning.";

    [Test]
    public async Task LogRetro_WhenTabOpen_ShouldAppearWithoutReload()
    {
        var key = await ArrangeStoryAsync();
        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, SeedIds.Sprints.Sprint24);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(retro.PointOf(PointText)).ToHaveCountAsync(0);

        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" });
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);
        var result = await mcp.CallToolAsync("log_retrospective", new Dictionary<string, object?>
        {
            ["item"] = key,
            ["went_well"] = new[] { new Dictionary<string, object?> { ["text"] = PointText } },
        });
        result.IsError.ShouldNotBe(true, result.StructuredContent?.ToString());

        await Expect(retro.PointOf(PointText)).ToBeVisibleAsync();
    }

    private async Task<string> ArrangeStoryAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Lock hand-over").WithSprint(SeedIds.Sprints.Sprint24).InsertAsync(connection);
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
    }
}
