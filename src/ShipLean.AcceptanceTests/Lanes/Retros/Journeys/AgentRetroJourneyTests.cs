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
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// A sub-agent finishes a story in Sprint 24 and logs its retro over MCP. The reply says what was stored, then the sprint's
// Retro tab shows the points by category and the card with the raw failure log by work item, and the story's own page shows the same card.
[TestFixture]
public class AgentRetroJourneyTests : SeededAcceptanceTest
{
    private const string WentWell = "Contract tests caught the drift early";
    private const string WentBadly = "The build lock was held for ten minutes";
    private const string RootCause = "Every agent queued behind one host lock";
    private const string Improve = "Give each agent its own build slot";
    private const string NotInPlan = "A schema change landed mid-sprint";
    private const string Friction = "Retried the build twice before it ran";

    [Test]
    public async Task LogRetro_WhenStoryClosed_ShouldShowOnSprintRetroTab()
    {
        var (key, title) = await ArrangeClosedStoryAsync();
        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" }, "Lane Agent");
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);

        var reply = await CallAsync(mcp, "log_retrospective", new()
        {
            ["item"] = key,
            ["went_well"] = new[] { new { text = WentWell } },
            ["went_badly"] = new[] { new { text = WentBadly, root_cause = RootCause } },
            ["improve"] = new[] { new { text = Improve } },
            ["not_in_plan"] = new[] { new { text = NotInPlan } },
            ["failure_log"] = new { blockers = new[] { "Blocked on the host lock" }, assumptions_disproved = new[] { "CI matches local" }, friction = new[] { Friction } },
        });

        reply.GetProperty("item").GetString().ShouldBe(key);
        reply.GetProperty("role").GetString().ShouldBe("subAgent");
        reply.GetProperty("points_added").GetInt32().ShouldBe(4);
        reply.GetProperty("retro_id").GetGuid().ShouldNotBe(Guid.Empty);

        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);

        // By category: each point sits in the column of its category.
        await ExpectInColumnAsync(retro, WentWell, "wentWell");
        await ExpectInColumnAsync(retro, WentBadly, "wentBadly");
        await ExpectInColumnAsync(retro, Improve, "improve");
        await ExpectInColumnAsync(retro, NotInPlan, "notInPlan");
        await Expect(CoverageOf(retro, title)).ToHaveAttributeAsync("data-status", "done");

        // By work item: the story's card, with the failure log collapsed until opened.
        await retro.ViewByItem.ClickAsync();
        var card = retro.Cards.Filter(new() { HasText = key });
        await Expect(card).ToHaveCountAsync(1);
        await Expect(card).ToContainTextAsync(WentBadly);
        await Expect(card).ToContainTextAsync(NotInPlan);
        await Expect(card.GetByTestId(RetroSharedTestIds.RetroCardLog)).ToHaveCountAsync(0);
        await card.GetByTestId(RetroSharedTestIds.RetroCardLogToggle).ClickAsync();
        await Expect(card.GetByTestId(RetroSharedTestIds.RetroCardLog)).ToContainTextAsync(Friction);

        // The work item page lists the retro; its row opens the item page, which shows the same points and the log.
        await Page.GotoAsync(Links.WorkItem("SHIP", key));
        var section = new WorkItemRetroSection(Page);
        await Expect(section.Rows).ToHaveCountAsync(1);
        await section.Rows.First.ClickAsync();
        var item = new RetroItemPage(Page);
        await item.Panel.WaitForAsync();
        await Expect(item.Board.NoteOf(WentWell).Root).ToHaveCountAsync(1);
        await Expect(item.Board.NoteOf(WentBadly).Root).ToContainTextAsync(RootCause);
        await Expect(item.Board.NoteOf(Improve).Root).ToHaveCountAsync(1);
        await Expect(item.Board.NoteOf(NotInPlan).Root).ToHaveCountAsync(1);
        await Expect(item.WriterChips.First).ToContainTextAsync("Lane Agent");
        await Expect(item.WriterChips.First).ToHaveAttributeAsync("data-role", "subAgent");
        await Expect(item.Log).ToHaveCountAsync(0);
        await item.LogToggle.ClickAsync();
        await Expect(item.Log).ToContainTextAsync(Friction);
    }

    // The point sits in exactly one column, the one of its category.
    private static async Task ExpectInColumnAsync(SprintRetroPage retro, string text, string category)
    {
        await Expect(retro.PointOf(text)).ToHaveCountAsync(1);
        var column = retro.Columns.Filter(new() { HasText = text });
        await Expect(column).ToHaveCountAsync(1);
        await Expect(column).ToHaveAttributeAsync("data-category", category);
    }

    private static ILocator CoverageOf(SprintRetroPage retro, string title) => retro.CoverageRows.Filter(new() { HasText = title });

    // Sprint 24 moves to the past; a new story sits in it and is completed inside its dates, as the metrics read the history.
    private async Task<(string Key, string Title)> ArrangeClosedStoryAsync()
    {
        const string title = "Agent retro journey story";
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE sprint SET start_date = (now() AT TIME ZONE 'utc')::date - 14, end_date = (now() AT TIME ZONE 'utc')::date - 1 WHERE id = @id",
            new { id = SeedIds.Sprints.Sprint24 });
        var today = await connection.ExecuteScalarAsync<DateTime>("SELECT (now() AT TIME ZONE 'utc')::date");
        var start = new DateTimeOffset(today.AddDays(-12), TimeSpan.Zero);
        var story = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(title));
        await RetroHistory.MoveToSprintAsync(connection, story, SeedIds.Sprints.Sprint24, start);
        await RetroHistory.CompleteAsync(connection, story, start.AddDays(4));
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
        return (key!, title);
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
