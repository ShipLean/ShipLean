using System.Text.Json;
using Dapper;
using ModelContextProtocol.Client;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.Retros;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// A feature has two stories in Sprint 24. The first closes and its lane agent logs a retro; the feature waits on the sprint tab
// until the last story closes. Then a conductor compiles the feature retro from both lanes and the feature shows it, with coverage done.
[TestFixture]
public class FeatureRetroJourneyTests : SeededAcceptanceTest
{
    private const string FeatureTitle = "Feature retro journey feature";
    private const string LaneAPoint = "Lane A found the schema gap late";
    private const string LaneBPoint = "Lane B reused the shared fixture";
    private const string CompiledPoint = "Agree contracts before lanes start";

    [Test]
    public async Task CompileFeatureRetro_WhenLastStoryClosed_ShouldShowOnFeatureAndCoverage()
    {
        var arranged = await ArrangeFeatureAsync();
        var scopes = new HashSet<string> { "shiplean:read", "shiplean:write" };
        await using var laneA = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), await Host.IssueAgentToken(TestUser.Joel, scopes, "Lane A"));
        await using var laneB = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), await Host.IssueAgentToken(TestUser.Joel, scopes, "Lane B"));
        await using var conductor = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), await Host.IssueAgentToken(TestUser.Joel, scopes, "Conductor"));

        await CallAsync(laneA, "log_retrospective", new() { ["item"] = arranged.KeyA, ["went_badly"] = new[] { new { text = LaneAPoint, root_cause = "No schema review" } } });

        // One story is still open, so the feature waits.
        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(retro.CoverageRows.Filter(new() { HasText = FeatureTitle })).ToHaveAttributeAsync("data-status", "waiting");

        await CloseStoryBAsync(arranged.StoryB);
        await CallAsync(laneB, "log_retrospective", new() { ["item"] = arranged.KeyB, ["went_well"] = new[] { new { text = LaneBPoint } } });

        var reply = await CallAsync(conductor, "log_retrospective", new()
        {
            ["item"] = arranged.FeatureKey,
            ["improve"] = new[] { new { text = CompiledPoint } },
            ["compiled_from"] = new[] { arranged.KeyA, arranged.KeyB },
        });

        reply.GetProperty("item").GetString().ShouldBe(arranged.FeatureKey);
        reply.GetProperty("role").GetString().ShouldBe("conductor");
        reply.GetProperty("points_added").GetInt32().ShouldBe(1);
        reply.GetProperty("warnings").GetArrayLength().ShouldBe(0);

        await retro.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(retro.CoverageRows.Filter(new() { HasText = FeatureTitle })).ToHaveAttributeAsync("data-status", "done");
        await Expect(retro.PointOf(CompiledPoint)).ToHaveCountAsync(1);

        await Page.GotoAsync(Links.WorkItem("SHIP", arranged.FeatureKey));
        var section = new WorkItemRetroSection(Page);
        await Expect(section.Cards).ToHaveCountAsync(1);
        await Expect(section.Section).ToContainTextAsync(CompiledPoint);
        await Expect(section.Section).ToContainTextAsync("Compiled from");
        await Expect(section.Section).ToContainTextAsync(arranged.KeyA);
        await Expect(section.Section).ToContainTextAsync(arranged.KeyB);
    }

    private sealed record Arranged(string FeatureKey, string KeyA, string KeyB, Guid StoryB);

    private static async Task<DateTimeOffset> SprintStartAsync(NpgsqlConnection connection)
    {
        var today = await connection.ExecuteScalarAsync<DateTime>("SELECT (now() AT TIME ZONE 'utc')::date");
        return new DateTimeOffset(today.AddDays(-12), TimeSpan.Zero);
    }

    // Sprint 24 moves to the past. Story A is completed inside it; story B sits in it, still open.
    private async Task<Arranged> ArrangeFeatureAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE sprint SET start_date = (now() AT TIME ZONE 'utc')::date - 14, end_date = (now() AT TIME ZONE 'utc')::date - 1 WHERE id = @id",
            new { id = SeedIds.Sprints.Sprint24 });
        var start = await SprintStartAsync(connection);
        var feature = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle(FeatureTitle));
        var storyA = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Feature retro lane A").WithParent(feature));
        var storyB = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Feature retro lane B").WithParent(feature));
        await RetroHistory.MoveToSprintAsync(connection, storyA, SeedIds.Sprints.Sprint24, start);
        await RetroHistory.MoveToSprintAsync(connection, storyB, SeedIds.Sprints.Sprint24, start);
        await RetroHistory.CompleteAsync(connection, storyA, start.AddDays(3));
        return new Arranged(await KeyAsync(connection, feature), await KeyAsync(connection, storyA), await KeyAsync(connection, storyB), storyB);
    }

    private async Task CloseStoryBAsync(Guid storyB)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await RetroHistory.CompleteAsync(connection, storyB, (await SprintStartAsync(connection)).AddDays(5));
    }

    private static async Task<string> KeyAsync(NpgsqlConnection connection, Guid id) =>
        (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }))!;

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
