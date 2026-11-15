using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.TaskChanges;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.Plans;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.TaskChanges;

// The plan's main end-to-end journey: an agent puts commits on a Task, a reviewer comments on a line and requests changes, the next
// agent reads the comment in its brief, answers it in report_task with a new commit, and the reviewer sees it resolved.
[TestFixture]
public class AgentReviewLoopTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    private string _taskKey = null!;
    private Guid _taskId;

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddFakeCodeHost();

    [OneTimeSetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        _taskKey = keys.Tasks.Values.First();
        _taskId = keys.Ids[_taskKey];
    }

    [Test]
    public async Task AgentCommits_WhenReviewerRequestsChanges_ShouldReachNextAgentAndBeAnswered()
    {
        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" });
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);
        var repo = FakeCodeHost.StorefrontRepo.WebUrl;

        // 1. The agent takes the task and puts its first commit on it.
        (await CallAsync(mcp, "pick_up", new() { ["item"] = _taskKey })).GetProperty("state").GetString().ShouldBe("Active");
        var added = await CallAsync(mcp, "add_commits", new()
        {
            ["item"] = _taskKey,
            ["commits"] = new[] { new { repo, sha = FakeCodeHost.Shas.First } },
        });
        added.GetProperty("added").GetArrayLength().ShouldBe(1);
        added.GetProperty("added")[0].GetProperty("sha").GetString().ShouldBe(FakeCodeHost.Shas.First);

        // 2. The person sees the commit in the Changes section, reviews, comments on a line and requests changes.
        var panel = new ChangesPanel(Page);
        await new WorkItemScreen(Page).OpenAsync(Project, _taskKey);
        await Expect(panel.Rows).ToHaveCountAsync(1);
        await panel.ReviewAsync();
        var changes = new ChangesPage(Page);
        await changes.Screen.WaitForAsync();
        await Expect(changes.DiffFiles.First).ToBeVisibleAsync();
        await changes.AddLineCommentAsync(0, 3);
        var posted = await changes.PostCommentAsync(_taskId, "Cover the zero-tax case.");
        posted.Ok.ShouldBeTrue();
        var commentId = (await posted.JsonAsync())!.Value.GetProperty("id").GetGuid();
        await Expect(changes.Threads).ToHaveCountAsync(1);

        var requested = await changes.RequestChangesAsync(_taskId);
        requested.Ok.ShouldBeTrue();

        // 3. The task is New and unassigned, History says so, and the next agent's brief lists the comment.
        var item = await CallAsync(mcp, "get_work_item", new() { ["item"] = _taskKey });
        item.GetProperty("state").GetString().ShouldBe("New");
        item.GetProperty("assignee").ValueKind.ShouldBe(JsonValueKind.Null);
        item.GetProperty("open_code_comments").GetArrayLength().ShouldBe(1);
        await new WorkItemScreen(Page).OpenAsync(Project, _taskKey);
        await Expect(new SideRailPanel(Page).Revisions.Filter(new() { HasText = "Changes requested · 1 comment" })).ToHaveCountAsync(1);
        var brief = await CallAsync(mcp, "get_brief", new() { ["item"] = _taskKey });
        var briefText = brief.GetProperty("markdown").GetString()!;
        briefText.ShouldContain("## Open code comments");
        briefText.ShouldContain(commentId.ToString());
        briefText.ShouldContain("Cover the zero-tax case.");

        // 4. With the Changes page open, the agent takes the task again and reports a new commit that answers the comment.
        await changes.OpenAsync(Project, _taskKey);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await CallAsync(mcp, "pick_up", new() { ["item"] = _taskKey });
        var report = await CallAsync(mcp, "report_task", new()
        {
            ["item"] = _taskKey,
            ["status"] = "partial",
            ["commits"] = new[] { new { repo, sha = FakeCodeHost.Shas.Second } },
            ["answered_comments"] = new[] { new { id = commentId, reply = "Added a zero-tax test." } },
        });
        report.GetProperty("commits_not_added").GetArrayLength().ShouldBe(0);

        // 5. The page shows the new-commit banner; after Reload the comment is resolved and carries the reply.
        await Expect(changes.NewCommitBanner).ToBeVisibleAsync();
        await changes.ReloadForNewCommitAsync();
        await Expect(changes.NewCommitBanner).ToHaveCountAsync(0);
        await changes.ShowAllCommentsAsync();
        var thread = changes.Threads.Filter(new() { HasText = "Cover the zero-tax case." });
        await Expect(thread).ToContainTextAsync("Added a zero-tax test.");
        await Expect(thread.GetByRole(AriaRole.Button, new() { Name = "Reopen" })).ToHaveCountAsync(1);
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
