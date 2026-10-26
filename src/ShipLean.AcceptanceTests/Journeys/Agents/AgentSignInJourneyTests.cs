using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Agents;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// An agent registers itself, the person signs in and consents in a real browser, the agent captures and picks up work
// and steps up to plan once, and Joel's open board follows along without a reload.
[TestFixture]
public class AgentSignInJourneyTests : SeededAcceptanceTest
{
    private const string AgentName = "Journey agent";
    private const string StoryOne = "Journey story one";
    private const string StoryTwo = "Journey story two";

    [Test]
    public async Task J9_AgentSignsIn_CapturesAndPicksUpLive()
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.View).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();

        using var agent = new AgentOAuthClient(BaseUrl, AgentName, 8123);
        await agent.RegisterAsync();

        // The person meets the agent in a browser of their own, signed out: sign-in, then consent.
        await using var browser = await Page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl });
        var first = await agent.AuthorizeAsync(
            browser,
            [AgentOAuthClient.Read, AgentOAuthClient.Write],
            TestUser.Joel.Email,
            TestUser.Joel.Password,
            async consent =>
            {
                await Expect(consent.Client).ToHaveTextAsync(AgentName);
                await Expect(consent.Host).ToHaveTextAsync("unverified");
                await Expect(consent.ScopeRead).ToBeCheckedAsync();
                await Expect(consent.ScopeWrite).ToBeCheckedAsync();
                await Expect(consent.ScopePlan).Not.ToBeCheckedAsync();
            });
        var token = first.GetProperty("access_token").GetString()!;

        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);
        var items = new[] { new { type = "story", title = StoryOne }, new { type = "story", title = StoryTwo } };
        var preview = await mcp.CallToolAsync("capture_work", new Dictionary<string, object?> { ["project"] = "SHIP", ["items"] = items });
        var plan = preview.StructuredContent!.Value.GetProperty("plan").GetString();
        var applied = await mcp.CallToolAsync("capture_work", new Dictionary<string, object?> { ["project"] = "SHIP", ["plan"] = plan });
        var created = applied.StructuredContent!.Value.GetProperty("items").EnumerateArray().ToList();
        created.Select(item => item.GetProperty("title").GetString()).ShouldBe([StoryOne, StoryTwo]);
        var keyOne = created[0].GetProperty("key").GetString()!;

        var newColumn = board.LaneOfTitled(StoryOne).GetByTestId(BoardTestIds.Cell).Nth(BoardMoves.NewColumn);
        await Expect(newColumn.GetByTestId(BoardTestIds.Card).Filter(new LocatorFilterOptions { HasText = StoryOne })).ToHaveCountAsync(1);
        await Expect(newColumn.GetByTestId(BoardTestIds.Card).Filter(new LocatorFilterOptions { HasText = StoryTwo })).ToHaveCountAsync(1);

        await mcp.CallToolAsync("pick_up", new Dictionary<string, object?> { ["item"] = keyOne });

        var started = board.LaneOfTitled(StoryOne).GetByTestId(BoardTestIds.Cell).Nth(BoardMoves.InProgressColumn).GetByTestId(BoardTestIds.Card)
            .Filter(new LocatorFilterOptions { HasText = StoryOne });
        await Expect(started).ToHaveCountAsync(1);
        await Expect(BoardMoves.Assignee(started)).ToHaveAttributeAsync("title", "Joel Dickson");

        // Planning needs a third scope: the call is refused with the step-up challenge, then the person consents again.
        var refused = await agent.CallRawAsync(token, "create_sprint", """{"project":"SHIP"}""");
        refused.Status.ShouldBe(System.Net.HttpStatusCode.Forbidden);
        refused.WwwAuthenticate.ShouldContain("error=\"insufficient_scope\"");
        refused.WwwAuthenticate.ShouldContain("scope=\"shiplean:plan shiplean:read shiplean:write\"");

        var second = await agent.AuthorizeAsync(
            browser,
            [AgentOAuthClient.Read, AgentOAuthClient.Write, AgentOAuthClient.Plan],
            consent: async consent => await Expect(consent.ScopePlan).ToBeCheckedAsync());
        await using var planner = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), second.GetProperty("access_token").GetString()!);
        var sprint = await planner.CallToolAsync("create_sprint", new Dictionary<string, object?> { ["project"] = "SHIP" });
        sprint.IsError.ShouldNotBe(true);
        sprint.StructuredContent!.Value.GetProperty("sprint").GetString().ShouldBe("Sprint 27");

        var connected = new ConnectedAgentsScreen(Page);
        await connected.OpenAsync();
        await Expect(connected.Row(AgentName)).ToHaveCountAsync(1);
        await Expect(connected.Row(AgentName).GetByTestId(AgentsTestIds.ConnectedScopes)).ToContainTextAsync("Plan");

        var item = new WorkItemScreen(Page);
        await item.OpenAsync("SHIP", keyOne);
        await Expect(new SideRailPanel(Page).Revisions.GetByTestId(WorkItemTestIds.RevisionWho).First)
            .ToHaveTextAsync($"Joel Dickson via {AgentName} (unverified)");
    }
}
