using System.Text.Json;
using ModelContextProtocol.Client;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.AcceptanceTests.Journeys.Asks;

// The loop decisions and questions exist for: an agent asks or records over MCP, the owner answers on the work item page,
// and the next agent reads the answer through get_work_item or get_brief. Each test seeds its own item, owned by Joel.
[TestFixture]
public class AsksJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string AgentName = "Claude Code";

    [Test]
    public async Task AsksJourney_WhenAgentAsksAndOwnerReplies_ShouldGiveNextAgentTheAnswer()
    {
        const string answer = "Use the merchant.prod account.";
        var item = await AskSeeding.SeedItemAsync(ConnectionString, title: "Asked item", assignee: SeedIds.Users.Joel);
        await using var mcp = await ConnectAgentAsync();
        var asked = await CallAsync(mcp, "ask", new() { ["item"] = item.Key, ["text"] = "Which merchant account do refunds use?" });
        asked.GetProperty("label").GetString().ShouldBe("Q1");

        await new WorkItemScreen(Page).OpenAsync(Project, item.Key);
        var panel = new DecisionsPanel(Page, item.Id);
        var reply = await panel.ReplyAsync(1, answer);
        reply.Ok.ShouldBeTrue($"{reply.Status} {reply.Url}");

        // Nothing is left open, so the section folds and its rows leave the DOM.
        await ExpectFoldedThenExpandAsync(panel);
        await Expect(panel.QuestionStateChip(1)).ToContainTextAsync("Answered");
        await Expect(panel.CommentAskTag("answer")).ToHaveTextAsync("Answer to Q1");

        await Page.ReloadAsync();
        await ExpectFoldedThenExpandAsync(panel);
        await Expect(panel.QuestionStateChip(1)).ToContainTextAsync("Answered");
        await Expect(panel.CommentAskTag("answer")).ToHaveTextAsync("Answer to Q1");

        var read = await CallAsync(mcp, "get_work_item", new() { ["key"] = item.Key });
        read.GetProperty("questions")[0].GetProperty("answer").GetProperty("body").GetString().ShouldBe(answer);
        read.GetProperty("open_questions").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task AsksJourney_WhenOwnerChangesConfirmedDecision_ShouldPostCommentForNextAgent()
    {
        const string question = "Which queue carries refund events?";
        const string alternative = "Postgres LISTEN/NOTIFY";
        // get_brief reads only a plan item, so the chain is built by hand: Feature, story, task.
        var feature = await AskSeeding.SeedItemAsync(ConnectionString, "feature", "Brief feature");
        var story = await AskSeeding.SeedItemAsync(ConnectionString, "story", "Brief story", feature.Id);
        var task = await AskSeeding.SeedItemAsync(ConnectionString, "task", "Brief task", story.Id, SeedIds.Users.Joel);
        await using var mcp = await ConnectAgentAsync();
        var recorded = await CallAsync(mcp, "record_decisions", new()
        {
            ["item"] = task.Key,
            ["decisions"] = JsonSerializer.SerializeToElement(new[]
            {
                new { question = "Which store holds refunds?", recommended = "Postgres", alternatives = Array.Empty<string>() },
                new { question, recommended = "Redis streams", alternatives = new[] { alternative, "RabbitMQ" } },
            }),
        });
        recorded.ValueKind.ShouldBe(JsonValueKind.Object);

        await new WorkItemScreen(Page).OpenAsync(Project, task.Key);
        var panel = new DecisionsPanel(Page, task.Id);
        var confirmed = await panel.ConfirmAllAsync();
        confirmed.Ok.ShouldBeTrue($"{confirmed.Status} {confirmed.Url}");
        await Expect(panel.Toasts.Filter(new() { HasText = "2 decisions confirmed as recommended" })).ToBeVisibleAsync();
        await ExpectFoldedThenExpandAsync(panel);
        await Expect(panel.DecisionRow(2)).ToHaveAttributeAsync("data-state", "confirmed");

        // Option 0 is the current answer; option 1 is the first alternative.
        var changed = await panel.ChangeAsync(2, 1, "Redis is not on the host yet.");
        changed.Ok.ShouldBeTrue($"{changed.Status} {changed.Url}");
        await Expect(panel.Toasts.Filter(new() { HasText = "D2 changed · comment posted for the next agent" })).ToBeVisibleAsync();
        await Expect(panel.DecisionRow(2)).ToHaveAttributeAsync("data-state", "changed");
        await Expect(panel.CommentAskTag("changed")).ToHaveTextAsync("D2 changed");

        await Page.ReloadAsync();
        await ExpectFoldedThenExpandAsync(panel);
        await Expect(panel.DecisionRow(2)).ToHaveAttributeAsync("data-state", "changed");
        await Expect(panel.CommentAskTag("changed")).ToHaveTextAsync("D2 changed");

        var brief = await CallAsync(mcp, "get_brief", new() { ["item"] = task.Key });
        var markdown = brief.GetProperty("markdown").GetString()!;
        markdown.ShouldContain("### " + task.Key);
        markdown.ShouldContain($"- D2 (changed, alternative 1): {question} → {alternative}");
    }

    [Test]
    public async Task AsksJourney_WhenAgentRecordsWhilePageOpen_ShouldAppearWithoutReload()
    {
        var item = await AskSeeding.SeedItemAsync(ConnectionString, title: "Live item", assignee: SeedIds.Users.Joel);
        await using var mcp = await ConnectAgentAsync();
        await new WorkItemScreen(Page).OpenAsync(Project, item.Key);
        var panel = new DecisionsPanel(Page, item.Id);
        await Expect(panel.Empty).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();

        await CallAsync(mcp, "record_decisions", new()
        {
            ["item"] = item.Key,
            ["decisions"] = JsonSerializer.SerializeToElement(new[]
            {
                new { question = "Which region hosts the queue?", recommended = "eu-west", alternatives = new[] { "us-east" } },
            }),
        });

        await Expect(panel.DecisionRow(1)).ToBeVisibleAsync();
        await Expect(panel.DecisionRow(1)).ToHaveAttributeAsync("data-state", "open");
        await Expect(panel.Empty).ToBeHiddenAsync();

        // A client registered without verification is stamped "Name (unverified)" (CommentStoreContract's rule).
        var read = await CallAsync(mcp, "get_work_item", new() { ["key"] = item.Key });
        read.GetProperty("decisions")[0].GetProperty("agent_name").GetString().ShouldBe($"{AgentName} (unverified)");
    }

    private async Task<McpClient> ConnectAgentAsync()
    {
        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" }, clientName: AgentName);
        return await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);
    }

    // A write that leaves nothing open folds the section (its rows leave the DOM); wait for the fold, then open it.
    private static async Task ExpectFoldedThenExpandAsync(DecisionsPanel panel)
    {
        await Expect(panel.FoldToggle).ToHaveAttributeAsync("aria-expanded", "false");
        await panel.ExpandAsync();
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
