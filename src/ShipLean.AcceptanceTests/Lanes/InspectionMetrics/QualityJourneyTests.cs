using System.Globalization;
using System.Text.Json;
using Dapper;
using ModelContextProtocol.Client;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.InspectionMetrics;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.InspectionMetrics;

// SHIP-1398 wave 5 end to end, in the empty project EMPT (Joel is its admin). Seeded in SQL: an earlier sprint (20 to 9 days ago)
// and the running one (6 days ago to a week ahead), a Task whose lead time ends at its commit 14 days ago, and a PR linked to it
// that merged 12 days ago. An agent files two Bugs over MCP today, the second marked found before release. In the browser the first
// gets a Caused by link to the Task (?addLink=causedBy) and is saved. Inspection › Quality then counts the escaping bug in the
// cause's sprint, not the one it was filed in, beside 100 per 100 merged PRs, and lists the found bug as found before release.
[TestFixture]
public class QualityJourneyTests : SeededAcceptanceTest
{
    private const string Project = "EMPT";
    private static readonly Guid Empt = SeedIds.Projects.Empt;

    private string _task = null!;
    private Guid _earlier;

    [OneTimeSetUp]
    public async Task SeedHistoryAsync()
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        var start = new DateTimeOffset(today.AddDays(-16).ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _earlier = await SprintAsync(connection, "Sprint 1", today.AddDays(-20), today.AddDays(-9));
        await SprintAsync(connection, "Sprint 2", today.AddDays(-6), today.AddDays(7));

        var task = await new WorkItemBuilder(Empt).WithType("task").WithTitle("Report date filter").WithCreatedAt(start.AddHours(-1))
            .WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @task AND rev = 1", new { task, at = start.AddHours(-1) });
        await new RevisionBuilder(task).WithRev(2).WithChangedAt(start).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{SeedIds.States.Empt.TaskActive}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = 2 WHERE id = @task", new { task, state = SeedIds.States.Empt.TaskActive });
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, link_type, title, url, committed_at) VALUES (gen_random_uuid(), @task, 'url', 'commit abc1234', @url, @at)",
            new { task, url = "https://git.example.test/acme/empt/commit/abc1234e1e1e1", at = start.AddDays(2) });
        const string pr = "https://github.com/acme/empt/pull/7";
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, link_type, url, title) VALUES (gen_random_uuid(), @task, 'url', @pr, 'PR')", new { task, pr });
        await connection.ExecuteAsync(
            """
            INSERT INTO link_state (url_key, url, state, as_of, source, merged_at)
            VALUES (link_url_key(@pr), @pr, 'merged', @mergedAt, 'github', @mergedAt)
            """, new { pr, mergedAt = start.AddDays(4) });
        _task = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @task", new { task }) ?? throw new InvalidOperationException();
    }

    // Mutation: in QualityAggregator, count every bug in the sprint it was filed (Sprint 1 reads 0 escaping, the bug row Sprint 2).
    [Test]
    public async Task Quality_WhenBugCausedByTaskInEarlierSprint_ShouldCountInThatSprint()
    {
        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" }, "Quality Agent");
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);

        // 1. An agent files the bugs: one escaping, one found before release.
        var escaping = await FileBugAsync(mcp, """{"type":"bug","title":"Date filter drops the last day"}""");
        var found = await FileBugAsync(mcp, """{"type":"bug","title":"Found in review","found_before_release":true}""");

        // 2. The escaping bug gets its Caused by link from the item page, preset by ?addLink=causedBy, and is saved.
        await Page.GotoAsync($"{Links.WorkItem(Project, escaping)}?addLink=causedBy");
        await Expect(Page.GetByTestId(WorkItemTestIds.LinkForm)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(WorkItemTestIds.LinkType)).ToHaveValueAsync("causedBy");
        await Page.GetByTestId(WorkItemTestIds.LinkKey).FillAsync(_task);
        await Page.GetByTestId(WorkItemTestIds.LinkSubmit).ClickAsync();
        await Expect(Page.GetByTestId(WorkItemTestIds.LinkCausedByRow)).ToContainTextAsync(_task);
        await Page.GetByTestId(WorkItemTestIds.Save).ClickAsync();
        await Expect(Page.GetByTestId(WorkItemTestIds.SaveState)).ToContainTextAsync("Saved");

        // The earlier sprint's short name as the view labels it, from the same view over MCP.
        var view = await CallAsync(mcp, "inspection_metrics", new() { ["project"] = Project });
        var earlier = view.GetProperty("periods").EnumerateArray().Single(p => p.GetProperty("id").GetString() == _earlier.ToString());
        var shortName = earlier.GetProperty("short_name").GetString()!;

        // 3. Inspection › Quality: the last finished sprint is the cause's, with one escaping bug and 100 per 100 merged PRs.
        await Page.GotoAsync(InspectionMetricsPage.Path(Project, "quality"));
        var quality = Page.GetByTestId(InspectionMetricsTestIds.Quality);
        var cards = quality.GetByTestId(InspectionMetricsTestIds.QualityCard);
        var escapingCard = cards.Filter(new() { HasTextString = "Escaping bugs" });
        await Expect(escapingCard).ToContainTextAsync("Sprint 1");
        await Expect(escapingCard.GetByTestId(InspectionMetricsTestIds.QualityCardValue)).ToHaveTextAsync("1");
        var per100Card = cards.Filter(new() { HasTextString = "Per 100 merged PRs" });
        await Expect(per100Card.GetByTestId(InspectionMetricsTestIds.QualityCardValue)).ToHaveTextAsync("100");

        // 4. The bug list: the escaping bug counts in the earlier sprint with its cause; the found one is marked as such.
        var rows = quality.GetByTestId(InspectionMetricsTestIds.QualityBugRow);
        var escapingRow = rows.And(Page.Locator($"[data-key='{escaping}']"));
        await Expect(escapingRow).ToContainTextAsync($"· {shortName}");
        await Expect(escapingRow.GetByTestId(InspectionMetricsTestIds.QualityBugCausedBy)).ToContainTextAsync(_task);
        var foundRow = rows.And(Page.Locator($"[data-key='{found}']"));
        await Expect(foundRow).ToHaveAttributeAsync("data-found-before-release", "true");
        await Expect(foundRow.GetByTestId(InspectionMetricsTestIds.QualityBugFoundBeforeRelease)).ToBeVisibleAsync();
    }

    private static async Task<Guid> SprintAsync(NpgsqlConnection connection, string name, DateOnly start, DateOnly end)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @Empt, @name, CAST(@from AS date), CAST(@to AS date))",
            new { id, Empt, name, from = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), to = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        return id;
    }

    private static async Task<string> FileBugAsync(McpClient mcp, string item)
    {
        var reply = await CallAsync(mcp, "capture_work", new() { ["project"] = Project, ["items"] = new[] { JsonDocument.Parse(item).RootElement } });
        return reply.GetProperty("items")[0].GetProperty("key").GetString()!;
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
