using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.InspectionMetrics;
using ShipLean.AcceptanceTests.Pages.Timeline;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.Plans;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.InspectionMetrics;

// SHIP-1398 wave 1 end to end. An agent works one task of a seeded plan over MCP: pick_up, the Blocked tag on and off, a review with
// changes requested, then report_task done with a commit whose committed_at is 5 h after the start, so the task resolves 5 h after
// its last commit. Each step's time is moved into the past in SQL (start 10 h ago, blocked 9-8 h ago, review 7 h ago, commit 5 h
// ago); the resolve stays now. The journey: Inspection's Lead time shows the sprint's mean, the sprint's rail ends the task at the
// last commit, and the Feature timeline's last-commit tick reads the same time (item D14). Expected numbers come from the
// inspection_metrics tool, which reads the same view as the page; the end itself is checked against the commit time.
[TestFixture]
public class LeadTimeJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string Sha = "e1e1e1e1aaaabbbbccccddddeeeeffff00001111";

    private string _feature = null!;
    private string _task = null!;
    private Guid _taskId;

    // As in AgentReviewLoopTests: lets PlanTreeSeed write as Joel from Host.Services. Requests still use the real request user.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<ActingCurrentUser>(sp => new ActingCurrentUser(sp, new CurrentUser(sp.GetRequiredService<ICurrentRequest>())));
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<ActingCurrentUser>());
    };

    [OneTimeSetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        _feature = keys.Feature;
        _task = keys.Tasks.Values.First();
        _taskId = keys.Ids[_task];
    }

    // Mutation: in TaskLeadTimeMeasure, end at the first resolve instead of the last commit.
    [Test]
    public async Task Lead_WhenTaskCommittedThenResolvedLater_ShouldEndAtLastCommitOnInspectionAndTimeline()
    {
        var now = TimeProvider.System.GetUtcNow();
        now = new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
        var committedAt = now.AddHours(-5);
        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" }, "Lead Agent");
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await BackdateAsync(connection, 0, now.AddHours(-11));
        var rev = await LastRevAsync(connection);
        await CallAsync(mcp, "pick_up", new() { ["item"] = _task });
        await BackdateAsync(connection, rev, now.AddHours(-10));
        var tags = (await CallAsync(mcp, "get_work_item", new() { ["key"] = _task })).GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToList();
        rev = await LastRevAsync(connection);
        await CallAsync(mcp, "refine_work_item", new() { ["item"] = _task, ["tags"] = tags.Append("blocked").ToArray() });
        await BackdateAsync(connection, rev, now.AddHours(-9));
        rev = await LastRevAsync(connection);
        await CallAsync(mcp, "refine_work_item", new() { ["item"] = _task, ["tags"] = tags.ToArray() });
        await BackdateAsync(connection, rev, now.AddHours(-8));
        await CallAsync(mcp, "log_review", new() { ["item"] = _task, ["verdict"] = "changes_requested" });
        await connection.ExecuteAsync("UPDATE review_event SET at = @at WHERE work_item_id = @id", new { id = _taskId, at = now.AddHours(-7) });
        await CallAsync(mcp, "report_task", new()
        {
            ["item"] = _task,
            ["status"] = "done",
            ["commits"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["sha"] = Sha,
                    ["subject"] = "Lead time journey",
                    ["url"] = $"https://git.example.test/acme/ship/commit/{Sha}",
                    ["committed_at"] = committedAt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                },
            },
        });

        // What the page should show, from the same view over MCP: the sprint the task ended in, its mean, and the task's end.
        var view = await CallAsync(mcp, "inspection_metrics", new() { ["project"] = Project });
        var period = view.GetProperty("periods").EnumerateArray()
            .Single(p => p.GetProperty("dots").EnumerateArray().Any(d => d.GetProperty("task_key").GetString() == _task));
        var periodId = period.GetProperty("id").GetString()!;
        var detail = await CallAsync(mcp, "inspection_metrics", new() { ["project"] = Project, ["period"] = periodId });
        var task = detail.GetProperty("period_detail").GetProperty("tasks").EnumerateArray().Single(t => t.GetProperty("key").GetString() == _task);
        task.GetProperty("ended_by").GetString().ShouldBe("lastCommit");
        task.GetProperty("ended_at").GetDateTimeOffset().ShouldBe(committedAt);
        task.GetProperty("lead_hours").GetDouble().ShouldBe(5d);
        var endedLabel = task.GetProperty("ended_label").GetString()!;

        // 2. Inspection's Lead time: the sprint's row shows its mean.
        var inspection = new InspectionMetricsPage(Page);
        await Page.GotoAsync(InspectionMetricsPage.Path(Project, "leadtime"));
        var row = inspection.LeadTime.GetByTestId(InspectionMetricsTestIds.PeriodRow).Filter(new() { HasTextString = period.GetProperty("name").GetString()! });
        await Expect(row).ToHaveAttributeAsync("data-period-id", periodId);
        await Expect(row).ToContainTextAsync(period.GetProperty("lead_time").GetProperty("mean_label").GetString()!);

        // 3. Open the sprint, select the task: the rail ends its lead time at the last commit.
        await row.ClickAsync();
        await Expect(inspection.Period).ToBeVisibleAsync();
        await inspection.Period.GetByTestId(InspectionMetricsTestIds.TaskRow).Filter(new() { HasTextString = _task }).ClickAsync();
        var rail = inspection.Period.GetByTestId(InspectionMetricsTestIds.Rail);
        await Expect(rail).ToHaveAttributeAsync("data-key", _task);
        var end = rail.GetByTestId(InspectionMetricsTestIds.RailEvent).Filter(new() { HasTextString = "Lead time ends" });
        await Expect(end).ToHaveAttributeAsync("data-kind", "end");
        await Expect(end).ToContainTextAsync("at the last commit");
        await Expect(end).ToContainTextAsync(endedLabel);

        // 4. The Feature timeline: the task's last-commit tick reads the same time.
        var timeline = new FeatureTimelinePage(Page);
        await Page.GotoAsync($"{Links.WorkItem(Project, _feature)}/timeline");
        await Expect(timeline.CommitTicks).ToHaveCountAsync(1);
        await Expect(timeline.CommitTicks).ToHaveAttributeAsync("data-key", _task);
        await Expect(timeline.CommitTicks).ToHaveTextAsync($"Last commit {endedLabel}");
    }

    private async Task<int> LastRevAsync(NpgsqlConnection connection)
        => await connection.QuerySingleAsync<int>("SELECT max(rev) FROM revision WHERE work_item_id = @id", new { id = _taskId });

    // Moves every revision of the task after afterRev to the given time.
    private async Task BackdateAsync(NpgsqlConnection connection, int afterRev, DateTimeOffset at)
        => await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev > @afterRev", new { id = _taskId, afterRev, at });

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
