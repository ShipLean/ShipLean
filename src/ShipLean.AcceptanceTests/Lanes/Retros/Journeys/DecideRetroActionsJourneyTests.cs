using System.Text.Json;
using Dapper;
using ModelContextProtocol.Client;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// An agent logs the retro of a story under a Feature, lists what is undecided, previews its decisions and applies them. A person
// who has the Feature's retro open sees the pinned action and the Not needed reason with the agent's name, without a reload.
[TestFixture]
public class DecideRetroActionsJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string AgentName = "Decider";
    private const string Reason = "The lock was held by a stuck job, not by the plan";
    private static readonly HashSet<string> Scopes = ["shiplean:read", "shiplean:write", "shiplean:plan"];

    [Test]
    public async Task Decide_WhenAgentRunsOnAFeature_ShouldShowActionsAndReasonsOnTheBoard()
    {
        var feature = await ArrangeFeatureAsync();
        await using var mcp = await ConnectAsync();
        var logged = await LogPointsAsync(mcp, feature.StoryKey, ("improve", "Give each agent its own build slot"), ("went_badly", "The build lock was held for ten minutes"));

        var undecided = await CallAsync(mcp, "list_retrospectives", new() { ["feature"] = feature.Key, ["undecided"] = true });
        var points = undecided.GetProperty("points").EnumerateArray().ToList();
        points.Count.ShouldBe(2);
        points.ShouldAllBe(p => p.GetProperty("decision").GetString() == "undecided");
        var improve = IdOf(points, "Give each agent its own build slot");
        var badly = IdOf(points, "The build lock was held for ten minutes");

        var board = new RetroItemPage(Page);
        await board.OpenAsync(Project, feature.StoryKey);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(board.Board.NoteOf("Give each agent its own build slot").Pin).ToHaveCountAsync(0);
        await Expect(board.Board.NoteOf("The build lock was held").None).ToHaveCountAsync(0);

        var arguments = new Dictionary<string, object?>
        {
            ["feature"] = feature.Key,
            ["actions"] = new[] { new { title = "Per-agent build slots", fix_type = "product", points = new[] { improve } } },
            ["not_needed"] = new[] { new { point = badly, reason = Reason } },
        };
        var preview = await CallAsync(mcp, "decide_retro_actions", arguments);
        preview.GetProperty("applied").GetBoolean().ShouldBeFalse();
        preview.GetProperty("would_create").GetArrayLength().ShouldBe(1);
        preview.GetProperty("would_mark_not_needed").GetArrayLength().ShouldBe(1);
        var plan = preview.GetProperty("plan").GetString()!;

        // Nothing changed on screen, and the server holds nothing new.
        await Expect(board.Board.NoteOf("Give each agent its own build slot").Pin).ToHaveCountAsync(0);
        await Expect(board.Board.NoteOf("The build lock was held").None).ToHaveCountAsync(0);
        (await CountActionsAsync("Per-agent build slots")).ShouldBe(0);

        var applied = await CallAsync(mcp, "decide_retro_actions", new() { ["plan"] = plan });
        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        var key = applied.GetProperty("created").EnumerateArray().Single().GetString()!;

        // A live event refreshes the story's open board (the Feature's own board holds only its own retro).
        await Expect(board.Board.NoteOf("Give each agent its own build slot").Pin).ToContainTextAsync(key);
        var none = board.Board.NoteOf("The build lock was held").None;
        await Expect(none).ToContainTextAsync("Not needed");
        await Expect(none).ToContainTextAsync(Reason);
        await Expect(none).ToContainTextAsync(AgentName);

        var created = await ReadItemAsync(key);
        created.State.ShouldBe("New");
        created.SprintId.ShouldBeNull();
        logged.ShouldBe(2);
    }

    [Test]
    public async Task Decide_WhenRunAgain_ShouldSkipWhatIsDecided()
    {
        var feature = await ArrangeFeatureAsync();
        await using var mcp = await ConnectAsync();
        await LogPointsAsync(mcp, feature.StoryKey, ("improve", "Run the slow suite nightly"), ("improve", "Name the owner of each lane"));
        var undecided = await CallAsync(mcp, "list_retrospectives", new() { ["feature"] = feature.Key, ["undecided"] = true });
        var points = undecided.GetProperty("points").EnumerateArray().ToList();
        var first = IdOf(points, "Run the slow suite nightly");
        var second = IdOf(points, "Name the owner of each lane");
        var firstRun = await CallAsync(mcp, "decide_retro_actions", new()
        {
            ["feature"] = feature.Key,
            ["not_needed"] = new[] { new { point = first, reason = "Already scheduled" } },
        });
        await CallAsync(mcp, "decide_retro_actions", new() { ["plan"] = firstRun.GetProperty("plan").GetString() });

        var again = await CallAsync(mcp, "decide_retro_actions", new()
        {
            ["feature"] = feature.Key,
            ["not_needed"] = new[] { new { point = first, reason = "Said again" }, new { point = second, reason = "Not a problem" } },
        });

        again.GetProperty("skipped").EnumerateArray().Select(s => s.GetProperty("point").GetGuid()).ShouldBe([first]);
        again.GetProperty("would_mark_not_needed").EnumerateArray().Select(s => s.GetProperty("point").GetGuid()).ShouldBe([second]);
        var remaining = await CallAsync(mcp, "list_retrospectives", new() { ["feature"] = feature.Key, ["undecided"] = true });
        remaining.GetProperty("points").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ShouldBe([second]);
    }

    [Test]
    public async Task Decide_WhenFourNew_ShouldBeRefused()
    {
        var feature = await ArrangeFeatureAsync();
        await using var mcp = await ConnectAsync();
        await LogPointsAsync(mcp, feature.StoryKey, ("improve", "One"), ("improve", "Two"), ("improve", "Three"), ("improve", "Four"));
        var undecided = await CallAsync(mcp, "list_retrospectives", new() { ["feature"] = feature.Key, ["undecided"] = true });
        var ids = undecided.GetProperty("points").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        ids.Count.ShouldBe(4);

        var result = await mcp.CallToolAsync("decide_retro_actions", new Dictionary<string, object?>
        {
            ["project"] = Project,
            ["feature"] = feature.Key,
            ["actions"] = ids.Select((id, i) => new { title = $"Action {i}", fix_type = "other", points = new[] { id } }).ToArray(),
        });

        result.IsError.ShouldBe(true);
        var text = result.StructuredContent?.ToString() ?? string.Join(" ", result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(b => b.Text));
        text.ShouldContain("actions");
        text.ShouldContain("3 new action items");
        (await CountActionsAsync("Action ")).ShouldBe(0);
    }

    [Test]
    public async Task Decide_WhenProposingAMerge_ShouldWaitOnInsights()
    {
        var feature = await ArrangeFeatureAsync();
        var (from, into) = ($"CI screenshots {Guid.NewGuid():N}", $"Screenshot baselines {Guid.NewGuid():N}");
        Guid pointId;
        await using (var connection = await OpenAsync())
        {
            var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
            var fromTheme = await seed.ThemeAsync(from);
            await seed.ThemeAsync(into);
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: feature.Story, AgentName: "seed-" + Guid.NewGuid().ToString("N")));
            pointId = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Baselines drift between runners", ThemeId: fromTheme));
        }

        await using var mcp = await ConnectAsync();
        var preview = await CallAsync(mcp, "decide_retro_actions", new()
        {
            ["feature"] = feature.Key,
            ["merges"] = new[] { new { from, into, reason = "Same thing" } },
        });
        preview.GetProperty("would_propose_merges").GetInt32().ShouldBe(1);
        var applied = await CallAsync(mcp, "decide_retro_actions", new() { ["plan"] = preview.GetProperty("plan").GetString() });
        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();

        // Only proposed: the point is still on its old theme, and the proposal waits for a person.
        await using (var connection = await OpenAsync())
        {
            (await connection.QuerySingleAsync<string>("SELECT t.name FROM retro_point p JOIN retro_theme t ON t.id = p.theme_id WHERE p.id = @pointId", new { pointId })).ShouldBe(from);
        }

        var insights = new RetroInsightsPage(Page);
        await insights.OpenAsync(Project);
        var proposal = Page.GetByTestId(RetrosTestIds.RetroMergeProposal).Filter(new() { HasText = from });
        await Expect(proposal).ToHaveCountAsync(1);
        await Expect(proposal).ToContainTextAsync(into);
        await Expect(proposal).ToContainTextAsync(AgentName);
        await Expect(proposal.GetByTestId(RetrosTestIds.RetroMergeProposalMerge)).ToBeVisibleAsync();
    }

    private sealed record FeatureWorld(Guid Story, string Key, string StoryKey);

    private sealed record ItemRow(string State, Guid? SprintId);

    // A Feature with one story; the retro points are logged on the story, so the Feature scope reaches them through its child.
    private async Task<FeatureWorld> ArrangeFeatureAsync()
    {
        await using var connection = await OpenAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle($"Decide journey feature {Guid.NewGuid():N}").InsertAsync(connection);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Decide journey story").WithParent(feature).InsertAsync(connection);
        return new FeatureWorld(
            story,
            await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature }),
            await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story }));
    }

    private async Task<McpClient> ConnectAsync() =>
        await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), await Host.IssueAgentToken(TestUser.Joel, Scopes, AgentName));

    private static async Task<int> LogPointsAsync(McpClient mcp, string itemKey, params (string Category, string Text)[] points)
    {
        var arguments = new Dictionary<string, object?> { ["item"] = itemKey };
        foreach (var group in points.GroupBy(p => p.Category))
        {
            arguments[group.Key] = group.Select(p => group.Key == "went_badly" ? (object)new { text = p.Text, root_cause = "A shared host lock" } : new { text = p.Text }).ToArray();
        }

        var reply = await CallAsync(mcp, "log_retrospective", arguments);
        return reply.GetProperty("points_added").GetInt32();
    }

    private static Guid IdOf(IEnumerable<JsonElement> points, string text) =>
        points.Single(p => p.GetProperty("text").GetString() == text).GetProperty("id").GetGuid();

    private async Task<int> CountActionsAsync(string titlePrefix)
    {
        await using var connection = await OpenAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item w JOIN retro_action ra ON ra.work_item_id = w.id WHERE w.title LIKE @prefix", new { prefix = titlePrefix + "%" });
    }

    private async Task<ItemRow> ReadItemAsync(string key)
    {
        await using var connection = await OpenAsync();
        var row = await connection.QuerySingleAsync<(string State, Guid? Sprint)>(
            "SELECT s.name AS State, w.sprint_id AS Sprint FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.key = @key", new { key });
        return new ItemRow(row.State, row.Sprint);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        arguments.TryAdd("project", Project);
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
