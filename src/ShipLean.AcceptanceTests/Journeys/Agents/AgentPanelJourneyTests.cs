using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// Joel moves a story into a state that starts an agent. The agent (a stub receiver: nothing listens, the host never sends)
// reports back through the Linear-compatible GraphQL endpoint with its callback token. The panel on the item shows what it
// said, and Stop queues the stop signal for the receiver.
[TestFixture]
public class AgentPanelJourneyTests : SeededAcceptanceTest
{
    private const string CreateAgentActivity =
        "mutation createAgentActivity($input: AgentActivityCreateInput!) { agentActivityCreate(input: $input) { success } }";

    private static readonly Dictionary<string, string> Client = new() { ["X-ShipLean-Client"] = "agent-panel-journey" };

    private async Task<JsonElement> ApiAsync(string method, string path, object? body = null)
    {
        var options = new APIRequestContextOptions { Method = method, Headers = Client, DataObject = body };
        var response = await Page.Context.APIRequest.FetchAsync(path, options);
        var text = await response.TextAsync();
        response.Ok.ShouldBeTrue(text);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task<T> QueryAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Test]
    public async Task AgentPanel_WhenTheAgentReportsAndJoelStops_ShouldShowItAndQueueTheStop()
    {
        var projectId = SeedIds.Projects.Ship;

        // Arrange: a connection (its token is the agent's), a repository, and a transition into "Active" with a Send to agent action.
        var connection = await ApiAsync("POST", "/api/instance/agent-connections", new { name = "Relay", receiverUrl = "https://relay.example.com/hook" });
        var token = connection.GetProperty("callbackToken").GetString()!;
        await ExecuteAsync(
            $"""
            UPDATE board SET settings = jsonb_set(settings, ARRAY['transitions'], jsonb_build_array(jsonb_build_object(
                'id', 'to-active', 'type', 'story', 'from', NULL,
                'to', (SELECT jsonb_build_object('stateId', s.id, 'name', s.name) FROM state s WHERE s.id = '{SeedIds.States.Ship.StoryActive}'),
                'actions', jsonb_build_array(jsonb_build_object('id', 'send', 'kind', 'agent', 'prompt', 'Do the thing.', 'connectionId', NULL)))))
            WHERE project_id = '{projectId}' AND level = 'stories';
            UPDATE project SET agent_connection_id = '{connection.GetProperty("connection").GetProperty("id").GetString()}',
                               repo_url = 'https://github.com/ShipLean/ShipLean.git', repo_base_branch = 'develop'
            WHERE id = '{projectId}';
            """);

        // 1. A new story moved into the flagged state opens a session.
        var item = await ApiAsync("POST", "/api/work-items", new { projectId, type = "story", title = "Add the thing" });
        var itemId = item.GetProperty("id").GetGuid();
        await ApiAsync("PATCH", $"/api/work-items/{itemId}", new
        {
            expectedRevision = item.GetProperty("revision").GetInt32(),
            changes = new { state = new { value = SeedIds.States.Ship.StoryActive } },
        });

        var screen = new WorkItemScreen(Page);
        await screen.OpenAsync("SHIP", item.GetProperty("key").GetString()!);
        var panel = Page.GetByTestId(AgentPanelTestIds.Panel);
        await Expect(panel).ToBeVisibleAsync();
        await Expect(panel.GetByTestId(AgentPanelTestIds.Connection)).ToHaveTextAsync("Relay");
        await Expect(panel.GetByTestId(AgentPanelTestIds.State)).ToHaveAttributeAsync("data-state", "submitted");
        var sessionId = await QueryAsync<Guid>("SELECT id FROM agent_session WHERE work_item_id = @item", ("item", itemId));

        // 2. The agent posts activities through GraphQL; the open panel follows live.
        using var agent = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        agent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await PostActivityAsync(agent, sessionId, new { type = "thought", body = "Reading the item" });
        await PostActivityAsync(agent, sessionId, new { type = "action", action = "Run", parameter = "dotnet test", result = "passed" });
        await Expect(panel.GetByTestId(AgentPanelTestIds.State)).ToHaveAttributeAsync("data-state", "working");
        await Expect(panel.GetByTestId(AgentPanelTestIds.Activity)).ToHaveCountAsync(2);
        await Expect(panel.GetByTestId(AgentPanelTestIds.Activities)).ToContainTextAsync("Reading the item");
        await Expect(panel.GetByTestId(AgentPanelTestIds.Activities)).ToContainTextAsync("dotnet test");

        // 3. Stop queues the stop signal for the receiver, and the panel says it is stopping.
        await Page.RunAndWaitForApiAsync($"/api/work-items/{itemId}/agent-session/stop", "POST", () => panel.GetByTestId(AgentPanelTestIds.Stop).ClickAsync());
        await Expect(panel.GetByTestId(AgentPanelTestIds.Stopping)).ToBeVisibleAsync();
        await Expect(panel.GetByTestId(AgentPanelTestIds.Stop)).ToHaveCountAsync(0);
        var queued = await QueryAsync<long>(
            "SELECT count(*) FROM agent_delivery WHERE session_id = @session AND action = 'prompted' AND status = 'pending' AND body #>> '{agentActivity,signal}' = 'stop'",
            ("session", sessionId));
        queued.ShouldBe(1);
    }

    private static async Task PostActivityAsync(HttpClient agent, Guid sessionId, object content)
    {
        using var response = await agent.PostAsJsonAsync("/linear/graphql", new { query = CreateAgentActivity, variables = new { input = new { agentSessionId = sessionId, content } } });
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        response.IsSuccessStatusCode.ShouldBeTrue(body.ToJsonString());
        body["errors"].ShouldBeNull(body.ToJsonString());
    }
}
