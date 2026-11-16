using System.Text;
using System.Text.Json.Nodes;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Demos;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.Contracts;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Demos;

// C5: the agent's session ends with its final response and no upload, so the open Demo tab turns Failed, quotes the first paragraph of
// that response, and Create demo again starts a new session straight away. Its own class so its database is its own; Sprint 23 is already closed.
[TestFixture]
public class DemoFailedJourneyTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeaders = new() { [ClientHeader.Name] = "acceptance-test" };

    private const string LastWords = "I couldn't make the Sprint 23 demo. My first Bash call was denied.\n\nMore detail.";

    [Test]
    [CancelAfter(240_000)]
    public async Task CreateDemo_WhenAgentEndsWithoutUploading_ShouldShowFailedAndAllowCreateAgain()
    {
        var callbackToken = await ArrangeAgentAsync();
        var sprint = SeedIds.Sprints.Sprint23;
        var tab = new SprintDemoTab(Page);
        await tab.GotoAsync("SHIP", sprint);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await tab.CreateDemoAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });
        var itemId = await ReadScalarAsync<Guid>($"SELECT demo_work_item_id FROM sprint WHERE id = '{sprint}'");
        var firstSession = await LatestSessionAsync(itemId);

        await RespondAsync(callbackToken, firstSession, LastWords);

        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "failed", new() { Timeout = 30_000 });
        await Expect(tab.StatusMessage).ToContainTextAsync("It said: \"I couldn't make the Sprint 23 demo. My first Bash call was denied.\"");
        await Expect(tab.StatusMessage).Not.ToContainTextAsync("More detail.");
        await Expect(tab.Create).ToHaveTextAsync("Create demo again");
        await Expect(tab.Create).ToBeEnabledAsync();

        await tab.CreateDemoAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });
        (await LatestSessionAsync(itemId)).ShouldNotBe(firstSession);
    }

    // A connection, the project's repository, the instance's address and the Demos setting off, all by API (as CreateDemoJourneyTests does).
    private async Task<string> ArrangeAgentAsync()
    {
        var connection = await SendAsync("POST", "/api/instance/agent-connections", new { name = $"Relay {Guid.NewGuid():N}", receiverUrl = "https://relay.example.com/hook" });
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

    // The agent's final response, through the connection's own callback token on the Linear-compatible endpoint: it ends the session with no upload.
    private async Task RespondAsync(string callbackToken, Guid sessionId, string responseBody)
    {
        const string Mutation = "mutation createAgentActivity($input: AgentActivityCreateInput!) { agentActivityCreate(input: $input) { success } }";
        var body = new JsonObject
        {
            ["query"] = Mutation,
            ["variables"] = new JsonObject { ["input"] = new JsonObject { ["agentSessionId"] = sessionId, ["content"] = new JsonObject { ["type"] = "response", ["body"] = responseBody } } },
        };
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/linear/graphql") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {callbackToken}");
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        JsonNode.Parse(text)!["data"]!["agentActivityCreate"]!["success"]!.GetValue<bool>().ShouldBeTrue(text);
    }

    private Task<Guid> LatestSessionAsync(Guid itemId) =>
        ReadScalarAsync<Guid>($"SELECT id FROM agent_session WHERE work_item_id = '{itemId}' ORDER BY created_at DESC LIMIT 1");

    private async Task<T> ReadScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"No row for: {sql}"));
    }
}
