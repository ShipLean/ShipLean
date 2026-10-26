using System.Text.Json;
using ModelContextProtocol.Client;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.Plans;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// An agent plans a feature for several agents over MCP: it reads the guide, captures #40's plan (preview, then apply), takes a
// lead's brief and a task's brief, reports the task done, and plan_status follows. Joel then sees the plan's stories in wave order.
[TestFixture]
public class AgentPlanFeatureJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    [Test]
    public async Task J11_PlanFromGuide_LandsAsFeatureStoriesAndTasks()
    {
        var fixture = PlanFixture.FeatureEditor;
        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" });
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);

        var guide = await CallAsync(mcp, "planning_guide", new() { ["project"] = Project });
        guide.GetProperty("part").GetString().ShouldBe("guide");
        guide.GetProperty("markdown").GetString()!.ShouldContain("capture_plan");

        var arguments = Arguments(fixture);
        var preview = await CallAsync(mcp, "capture_plan", arguments);
        preview.GetProperty("applied").GetBoolean().ShouldBeFalse();
        preview.GetProperty("items").GetInt32().ShouldBe(fixture.ItemCount);
        preview.GetProperty("warnings").GetArrayLength().ShouldBe(0);

        arguments["plan"] = preview.GetProperty("plan").GetString();
        var applied = await CallAsync(mcp, "capture_plan", arguments);
        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        var featureKey = applied.GetProperty("feature").GetString()!;
        var stories = applied.GetProperty("stories").EnumerateArray().ToList();
        stories.Select(s => s.GetProperty("title").GetString()).ShouldBe(fixture.Stories.Select(s => s.Title));
        stories.Sum(s => s.GetProperty("tasks").GetArrayLength()).ShouldBe(fixture.Stories.Sum(s => s.Tasks.Count));

        var laneF = fixture.Stories.Single(s => s.Lane == "F");
        var laneFStory = stories.Single(s => s.GetProperty("lane").GetString() == "F");
        var laneFTasks = laneFStory.GetProperty("tasks").EnumerateArray().ToList();
        laneFTasks.Select(t => t.GetProperty("title").GetString()).ShouldBe(laneF.Tasks.Select(t => t.Title));

        // The lead's brief lists F1-F5 by key with their model roles and context sizes, and carries the Feature's Contracts.
        var lead = await CallAsync(mcp, "get_brief", new() { ["item"] = laneFStory.GetProperty("key").GetString() });
        lead.GetProperty("kind").GetString().ShouldBe("story");
        var leadBrief = lead.GetProperty("markdown").GetString()!;
        foreach (var (task, row) in laneF.Tasks.Zip(laneFTasks))
        {
            leadBrief.ShouldContain($"| {row.GetProperty("key").GetString()} | {task.Title} | {task.Model} | {task.ContextK}k |");
        }
        leadBrief.ShouldContain("**C1 — Feature fields.**");

        // A task's brief is its agent's whole prompt: the common brief in front of the task's own brief.
        var f1Key = laneFTasks[0].GetProperty("key").GetString()!;
        var f1 = await CallAsync(mcp, "get_brief", new() { ["item"] = f1Key });
        f1.GetProperty("kind").GetString().ShouldBe("task");
        var f1Brief = f1.GetProperty("markdown").GetString()!;
        var common = f1Brief.IndexOf(fixture.CommonBrief.Trim(), StringComparison.Ordinal);
        var own = f1Brief.IndexOf(laneF.Tasks[0].Brief.Trim(), StringComparison.Ordinal);
        common.ShouldBeGreaterThanOrEqualTo(0);
        own.ShouldBeGreaterThan(common);
        f1Brief.ShouldContain(fixture.SharedBranch);

        var report = await CallAsync(mcp, "report_task", new()
        {
            ["item"] = f1Key,
            ["status"] = "done",
            ["commits"] = new[] { new { sha = "0123456789abcdef", subject = "Goal field", url = "https://example.com/commit/0123456789abcdef" } },
            ["tests_run"] = new[] { "GoalFieldTests — 3 passed — 2 s" },
        });
        report.GetProperty("links_added").GetInt32().ShouldBe(1);

        var status = await CallAsync(mcp, "plan_status", new() { ["item"] = featureKey });
        var f1Status = status.GetProperty("waves").EnumerateArray()
            .SelectMany(w => w.GetProperty("stories").EnumerateArray())
            .SelectMany(s => s.GetProperty("tasks").EnumerateArray())
            .Single(t => t.GetProperty("key").GetString() == f1Key);
        f1Status.GetProperty("category").GetString().ShouldBe("completed");
        f1Status.GetProperty("finished_at").ValueKind.ShouldBe(JsonValueKind.String);
        f1Status.GetProperty("commits").GetArrayLength().ShouldBe(1);
        status.GetProperty("startable_wave").GetString().ShouldBe("0.1");

        // Joel's Stories backlog lists the plan's stories in wave order.
        var backlog = new StoriesBacklog(Page);
        await backlog.OpenAsync(Project);
        var storyKeys = stories.Select(s => s.GetProperty("key").GetString()!).ToList();
        await Expect(backlog.RowKeys.Filter(new() { HasText = storyKeys[^1] })).ToHaveCountAsync(1);
        var shown = (await backlog.RowKeys.AllInnerTextsAsync()).Select(k => k.Trim()).Where(storyKeys.Contains).ToList();
        shown.ShouldBe(storyKeys);
    }

    // The fixture's capture_plan arguments for SHIP, as values the MCP client serialises as they are.
    private static Dictionary<string, object?> Arguments(PlanFixture fixture)
    {
        var arguments = fixture.Arguments().ToDictionary(p => p.Key, p => (object?)JsonSerializer.SerializeToElement(p.Value));
        arguments["project"] = Project;
        return arguments;
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
