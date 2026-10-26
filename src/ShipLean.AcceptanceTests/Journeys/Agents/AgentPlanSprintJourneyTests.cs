using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// An agent plans the next sprint over MCP (preview, then apply) while Joel has the Planning screen open: the sprint's
// section follows without a reload, and the agent's sprint_status and the Sprint backlog page agree with it.
[TestFixture]
public class AgentPlanSprintJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    [Test]
    public async Task J10_AgentPlansSprint_PlanningScreenMatches()
    {
        var planning = new PlanningBacklog(Page);
        var rows = new PlanningDragging(Page);
        await planning.OpenAsync(Project);
        await new LiveStatus(Page).WaitUntilLiveAsync();

        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write", "shiplean:plan" });
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);

        var preview = await CallAsync(mcp, "plan_sprint", new() { ["project"] = Project });
        preview.GetProperty("applied").GetBoolean().ShouldBeFalse();
        var sprintName = preview.GetProperty("sprint").GetString()!;
        var sprintId = SprintIdOf(preview);
        var planned = Keys(preview);
        planned.ShouldNotBeEmpty();
        var alreadyIn = preview.GetProperty("already_in_sprint").GetDecimal();
        var before = await rows.KeysInAsync(sprintId);
        before.Intersect(planned).ShouldBeEmpty();
        await Expect(planning.Committed(sprintName)).ToHaveTextAsync(Leading(alreadyIn));

        var applied = await CallAsync(mcp, "plan_sprint", new() { ["project"] = Project, ["plan"] = preview.GetProperty("plan").GetString() });
        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        Keys(applied).ShouldBe(planned);
        applied.GetProperty("items_points").GetDecimal().ShouldBe(preview.GetProperty("items_points").GetDecimal());
        var total = alreadyIn + applied.GetProperty("items_points").GetDecimal();

        // No reload: the open Planning screen takes the new rows into the sprint's section.
        foreach (var key in planned)
        {
            await Expect(rows.Row(key)).ToHaveAttributeAsync("data-section", sprintId);
        }

        (await rows.KeysInAsync(sprintId)).OrderBy(k => k).ShouldBe(before.Concat(planned).OrderBy(k => k));
        await Expect(planning.Committed(sprintName)).ToHaveTextAsync(Leading(total));

        var status = await CallAsync(mcp, "sprint_status", new() { ["project"] = Project, ["sprint"] = sprintName });
        status.GetProperty("committed").GetDecimal().ShouldBe(total);
        var statusItems = status.GetProperty("states").EnumerateArray()
            .SelectMany(g => g.GetProperty("items").EnumerateArray())
            .Where(i => i.GetProperty("type").GetString() != "task")
            .ToList();
        statusItems.Count.ShouldBe(before.Count + planned.Count);

        var sprintPage = new SprintBacklogPage(Page);
        await sprintPage.OpenAsync(Project, Guid.Parse(sprintId));
        await Expect(sprintPage.Committed).ToHaveTextAsync(Leading(total));
        await Expect(sprintPage.StoryRows).ToHaveCountAsync(statusItems.Count);
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }

    private static List<string> Keys(JsonElement result) =>
        [.. result.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];

    // The sprint's id is the path segment after /sprints/ in the reply's open_url.
    private static string SprintIdOf(JsonElement result) =>
        Regex.Match(result.GetProperty("open_url").GetString()!, "/sprints/([0-9a-f-]{36})").Groups[1].Value;

    // The figure a tile leads with: the points, not followed by more digits.
    private static Regex Leading(decimal points) => new($"^{Regex.Escape(Format(points))}(?![\\d.])");

    private static string Format(decimal points) => points.ToString("0.##", CultureInfo.InvariantCulture);
}
