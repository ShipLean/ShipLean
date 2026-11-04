using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Demos;
using ShipLean.AcceptanceTests.Pages.QuickSearch;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.Contracts;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Demos;

// A demo's BAU item is not work anyone plans, so no list shows it: not the backlogs, the boards or quick search. It still exists: the Demo tab links to it, and its page
// has the agent panel and the comments. And on a host with no YouTube OAuth client, the Demos settings say YouTube is off and every YouTube route is 404.
[TestFixture]
public class BauHiddenJourneyTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeaders = new() { [ClientHeader.Name] = "acceptance-test" };

    // A seeded story (Sign in with passkey) that every list below shows: the positive control for each "not here" check.
    private const string ControlKey = "SHIP-129";

    [Test]
    [CancelAfter(240_000)]
    public async Task BauItem_ShouldNotShowInBacklogBoardOrQuickSearchButOpensFromTheDemoTab()
    {
        var callbackToken = await ArrangeAgentAsync();
        var sprint = SeedIds.Sprints.Sprint23;
        var tab = new SprintDemoTab(Page);
        await tab.GotoAsync("SHIP", sprint);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await tab.CreateDemoAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });
        var itemKey = await tab.ItemLink.InnerTextAsync();
        var itemId = await ReadScalarAsync<Guid>($"SELECT demo_work_item_id FROM sprint WHERE id = '{sprint}'");
        (await ReadScalarAsync<string>($"SELECT type FROM work_item WHERE id = '{itemId}'")).ShouldBe("bau");
        (await ReadScalarAsync<string>($"SELECT key FROM work_item WHERE id = '{itemId}'")).ShouldBe(itemKey);

        // Put the item in a column of the stories board too (the run leaves it off every board), so the boards below are kept clean by the hiding rule and not by it being unplaced.
        await ExecuteAsync($"UPDATE work_item SET board_column_id = (SELECT board_column_id FROM work_item WHERE key = '{ControlKey}') WHERE id = '{itemId}'");

        // A full index pass, so the new item is in the search index and only the hiding rule keeps it out of the answers below.
        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISearchIndexer>().RunPassAsync(true);
        }

        // The stories backlog, in its list and in planning mode (where an unplanned item would sit under Backlog).
        var backlog = new StoriesBacklog(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Row(ControlKey)).ToBeVisibleAsync();
        await Expect(backlog.Row(itemKey)).ToHaveCountAsync(0);
        (await backlog.RowKeys.AllInnerTextsAsync()).ShouldNotContain(itemKey);
        var stack = await backlog.StackAsync();
        stack.Select(e => e.Key).ShouldContain(ControlKey);
        stack.Select(e => e.Key).ShouldNotContain(itemKey);
        stack.Select(e => e.Type).ShouldNotContain("bau");
        await Page.GotoAsync(Links.Backlog("SHIP", "stories", "planning"));
        await Expect(new PlanningBacklog(Page).Section("Backlog")).ToBeVisibleAsync();
        var unplanned = await backlog.SectionAsync("Backlog");
        unplanned.Select(e => e.Title).ShouldContain("Gift card balance check");
        unplanned.Select(e => e.Key).ShouldNotContain(itemKey);

        // The stories board and the features board.
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.Card(ControlKey)).ToBeVisibleAsync();
        await Expect(board.Card(itemKey)).ToHaveCountAsync(0);
        await Expect(board.CardTitled("Sprint 23 demo")).ToHaveCountAsync(0);
        await board.OpenAsync("SHIP", "features");
        await Expect(board.Card("SHIP-101")).ToBeVisibleAsync();
        await Expect(board.CardTitled("Sprint 23 demo")).ToHaveCountAsync(0);

        // Quick search by key and by title: the answer does not carry the item, and the dialog shows no row for it. A story from the same project is found the same way.
        var search = new QuickSearchDialog(Page);
        await search.OpenWithShortcutAsync();
        var byKey = await search.TypeAsync(itemKey);
        (await ItemKeysIn(byKey)).ShouldNotContain(itemKey);
        await Expect(search.Item(itemKey)).ToHaveCountAsync(0);
        var byTitle = await search.TypeAsync("Sprint 23 demo");
        (await ItemKeysIn(byTitle)).ShouldNotContain(itemKey);
        await Expect(search.Item(itemKey)).ToHaveCountAsync(0);
        var control = await search.TypeAsync("passkey");
        (await ItemKeysIn(control)).ShouldContain(ControlKey);
        await Expect(search.Item(ControlKey)).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");

        // Hidden is not gone: the Demo tab links to the item, and its page opens with the agent panel and the comments.
        await tab.GotoAsync("SHIP", sprint);
        await Expect(tab.ItemLink).ToHaveTextAsync(itemKey);
        await tab.ItemLink.ClickAsync();
        var screen = new WorkItemScreen(Page);
        await screen.Screen.WaitForAsync();
        await Expect(screen.Title).ToHaveValueAsync("Sprint 23 demo");
        Page.Url.ShouldEndWith(Links.WorkItem("SHIP", itemKey));

        var panel = Page.GetByTestId(AgentPanelTestIds.Panel);
        await Expect(panel).ToBeVisibleAsync();
        await Expect(panel.GetByTestId(AgentPanelTestIds.Connection)).ToHaveTextAsync("Relay");
        await Expect(panel.GetByTestId(AgentPanelTestIds.State)).ToHaveAttributeAsync("data-state", "submitted");
        var sessionId = await ReadScalarAsync<Guid>($"SELECT id FROM agent_session WHERE work_item_id = '{itemId}'");
        await PostActivityAsync(callbackToken, sessionId, new { type = "thought", body = "Reading the sprint's items" });
        await Expect(panel.GetByTestId(AgentPanelTestIds.State)).ToHaveAttributeAsync("data-state", "working");
        await Expect(panel.GetByTestId(AgentPanelTestIds.Activities)).ToContainTextAsync("Reading the sprint's items");

        var discussion = new DiscussionPanel(Page);
        var posted = await discussion.PostAsync(itemId, "The demo should start with checkout.");
        posted.Ok.ShouldBeTrue(await posted.TextAsync());
        await Expect(discussion.Bodies.Filter(new() { HasText = "The demo should start with checkout." })).ToHaveCountAsync(1);
        await Page.ReloadAsync();
        await screen.Screen.WaitForAsync();
        await Expect(discussion.Bodies.Filter(new() { HasText = "The demo should start with checkout." })).ToHaveCountAsync(1);
        await Expect(panel.GetByTestId(AgentPanelTestIds.Activities)).ToContainTextAsync("Reading the sprint's items");
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Settings_WhenInstanceHasNoClient_ShouldSayYouTubeIsOff()
    {
        var settings = new DemoSettingsTab(Page);
        await settings.GotoAsync("SHIP");
        await Expect(settings.YouTubePanel).ToBeVisibleAsync();
        await Expect(settings.YouTubeOff).ToContainTextAsync("YouTube isn't set up");
        await Expect(settings.YouTubeConnect).ToHaveCountAsync(0);
        await Expect(settings.YouTubeCheck).ToHaveCountAsync(0);

        // Every YouTube route is 404 while off, even for the admin.
        var start = await Page.Context.APIRequest.PostAsync($"/api/projects/{SeedIds.Projects.Ship}/youtube/start", new() { Headers = ClientHeaders });
        start.Status.ShouldBe(404, await start.TextAsync());
        var check = await Page.Context.APIRequest.PostAsync($"/api/projects/{SeedIds.Projects.Ship}/youtube/check", new() { Headers = ClientHeaders });
        check.Status.ShouldBe(404, await check.TextAsync());

        // The instance's Integrations tab has the client fields empty, and not from the environment.
        await new InstanceIntegrationsPage(Page).OpenAsync();
        await Expect(settings.InstanceClientId).ToBeVisibleAsync();
        await Expect(settings.InstanceClientId).ToHaveValueAsync("");
        await Expect(settings.InstanceFromEnv).ToContainTextAsync("Not set by environment");

        // Saving a client turns it on: the project's panel offers Connect, and the routes answer. So "off" is the missing client and nothing else.
        await settings.InstanceClientId.FillAsync("acceptance-client.apps.example.test");
        await settings.InstanceClientSecret.FillAsync("acceptance-secret");
        await new InstanceIntegrationsPage(Page).SaveAsync();
        await settings.GotoAsync("SHIP");
        await Expect(settings.YouTubeConnect).ToBeEnabledAsync();
        await Expect(settings.YouTubeOff).ToHaveCountAsync(0);
        var started = await Page.Context.APIRequest.PostAsync($"/api/projects/{SeedIds.Projects.Ship}/youtube/start", new() { Headers = ClientHeaders });
        started.Status.ShouldNotBe(404, await started.TextAsync());
    }

    // A connection, the project's repository, the instance's address (so the upload link reaches this host) and the Demos setting off, all by API.
    private async Task<string> ArrangeAgentAsync()
    {
        var connection = await SendAsync("POST", "/api/instance/agent-connections", new { name = "Relay", receiverUrl = "https://relay.example.com/hook" });
        var connectionId = connection!["connection"]!["id"]!.GetValue<Guid>();
        var callbackToken = connection["callbackToken"]!.GetValue<string>();

        var instance = (await SendAsync("GET", "/api/instance/settings"))!;
        instance["baseUrl"] = BaseUrl;
        await SendAsync("PUT", "/api/instance/settings", instance);

        var project = (await SendAsync("GET", $"/api/projects/{SeedIds.Projects.Ship}/settings"))!;
        var general = project["general"]!;
        var put = new JsonObject
        {
            ["general"] = new JsonObject
            {
                ["key"] = general["key"]!.DeepClone(), ["name"] = general["name"]!.DeepClone(), ["description"] = general["description"]!.DeepClone(),
                ["workingDays"] = general["workingDays"]!.DeepClone(), ["holidayCountry"] = general["holidayCountry"]?.DeepClone(),
                ["agentConnectionId"] = connectionId, ["repoUrl"] = "https://github.com/ShipLean/ShipLean.git", ["repoBaseBranch"] = "develop",
            },
            ["members"] = new JsonArray([.. project["members"]!.AsArray().Select(m => (JsonNode)new JsonObject { ["email"] = m!["email"]!.DeepClone(), ["role"] = m["role"]!.DeepClone() })]),
            ["sprints"] = project["sprints"]!.DeepClone(),
            ["states"] = new JsonObject { ["add"] = new JsonArray(), ["update"] = new JsonArray(), ["reorder"] = new JsonArray(), ["remove"] = new JsonArray() },
        };
        await SendAsync("PUT", $"/api/projects/{SeedIds.Projects.Ship}/settings", put);

        await SendAsync("PUT", $"/api/projects/{SeedIds.Projects.Ship}/demo-settings", new { automatic = false, environment = (string?)null });
        return callbackToken;
    }

    private async Task<JsonNode?> SendAsync(string method, string path, object? body = null)
    {
        var options = new APIRequestContextOptions { Method = method, Headers = ClientHeaders };
        if (body is not null)
        {
            options.DataObject = body;
        }

        var response = await Page.Context.APIRequest.FetchAsync(path, options);
        var text = await response.TextAsync();
        response.Ok.ShouldBeTrue($"{method} {path} answered {response.Status}: {text}");
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }

    // The agent's activity through the connection's own callback token on the Linear-compatible endpoint: nothing else here plays the agent.
    private async Task PostActivityAsync(string callbackToken, Guid sessionId, object content)
    {
        const string Mutation = "mutation createAgentActivity($input: AgentActivityCreateInput!) { agentActivityCreate(input: $input) { success } }";
        using var agent = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        agent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", callbackToken);
        using var response = await agent.PostAsJsonAsync("/linear/graphql", new { query = Mutation, variables = new { input = new { agentSessionId = sessionId, content } } });
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        var body = JsonNode.Parse(text)!;
        body["errors"].ShouldBeNull(text);
        body["data"]!["agentActivityCreate"]!["success"]!.GetValue<bool>().ShouldBeTrue(text);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> ItemKeysIn(IResponse search) =>
        [.. JsonNode.Parse(await search.TextAsync())!["items"]!.AsArray().Select(i => i!["key"]!.GetValue<string>())];

    private async Task<T> ReadScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"No row for: {sql}"));
    }
}
