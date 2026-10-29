using System.Text.Json;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.BoardSettings;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// Joel automates the Features board: New to Idea sends the feature to an agent with his prompt. A person moves a feature and a session opens whose
// delivered context (the stub receiver: nothing listens, the delivery stays queued) carries the prompt and the item. Removing Idea marks the
// transition Invalid and adding it back makes it valid again. Starting a story with no sprint puts it in the current sprint.
[TestFixture]
public class AutomationsJourneyTests : SeededAcceptanceTest
{
    private const string Prompt = "Draft a plan for this idea.";

    private static readonly Dictionary<string, string> Client = new() { ["X-ShipLean-Client"] = "automations-journey" };

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

    private async Task AddIdeaStateAsync(SettingsStatesPage states)
    {
        await states.OpenAsync();
        await Page.GetByTestId(SettingsTestIds.StateTypeFeature).ClickAsync();
        await Page.GetByTestId(SettingsTestIds.StateAddName).FillAsync("Idea");
        await Page.GetByTestId(SettingsTestIds.StateAdd).ClickAsync();
        (await states.SaveAsync()).Ok.ShouldBeTrue();
        await Expect(states.Unsaved).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Automations_WhenAFeatureMovesAndStatesChange_ShouldSendToAgentAndKeepTransitionsHonest()
    {
        var projectId = SeedIds.Projects.Ship;

        // Arrange: a connection and a repository for SHIP, and a feature state called Idea.
        var connection = await ApiAsync("POST", "/api/instance/agent-connections", new { name = "Relay", receiverUrl = "https://relay.example.com/hook" });
        await ExecuteAsync(
            $"""
            UPDATE project SET agent_connection_id = '{connection.GetProperty("connection").GetProperty("id").GetString()}',
                               repo_url = 'https://github.com/ShipLean/ShipLean.git', repo_base_branch = 'develop'
            WHERE id = '{projectId}';
            """);
        var states = new SettingsStatesPage(Page);
        await AddIdeaStateAsync(states);

        // 1. An admin adds New to Idea with a prompt on the Features board.
        var automations = new AutomationsPane(Page);
        await automations.OpenAsync("SHIP", "features");
        await Expect(automations.Pane).ToBeVisibleAsync();
        await automations.AddAsync("New", "Idea");
        await Expect(automations.Rows).ToHaveCountAsync(1);
        await Expect(automations.Invalid).ToHaveCountAsync(0);
        await automations.Prompt(0).FillAsync(Prompt);
        await automations.SaveAsync(SeedIds.Boards.ShipFeatures);

        // 2. A person moves a feature into Idea: a session opens, and the delivery's context has the prompt as guidance and the item.
        var feature = await ApiAsync("POST", "/api/work-items", new { projectId, type = "feature", title = "Offline mode" });
        var featureId = feature.GetProperty("id").GetGuid();
        var featureKey = feature.GetProperty("key").GetString()!;
        var idea = await QueryAsync<Guid>("SELECT id FROM state WHERE project_id = @project AND type = 'feature' AND name = 'Idea'", ("project", projectId));
        await ApiAsync("PATCH", $"/api/work-items/{featureId}", new
        {
            expectedRevision = feature.GetProperty("revision").GetInt32(),
            changes = new { state = new { value = idea } },
        });
        // The trigger runs after the move commits: wait, in the page, until the item's agent panel lists the session.
        await Page.WaitForFunctionAsync(
            "async id => (await (await fetch(`/api/work-items/${id}/agent-sessions`)).json()).sessions.length > 0", featureId.ToString());
        var body = await DeliveredContextAsync(featureId);
        var context = body.GetProperty("promptContext").GetString()!;
        context.ShouldContain("<guidance>");
        context.ShouldContain(Prompt);
        context.ShouldContain("Offline mode");
        context.ShouldContain(featureKey);
        body.GetProperty("guidance")[0].GetProperty("body").GetString()!.ShouldContain(Prompt);

        // 3. Removing Idea shows the transition as Invalid, and adding it back makes it valid.
        await states.OpenAsync();
        await Page.GetByTestId(SettingsTestIds.StateTypeFeature).ClickAsync();
        await states.DeleteOf(await RowOfAsync(states, "Idea")).ClickAsync();
        await states.ChooseTargetAsync("New");
        await states.DeleteConfirm.ClickAsync();
        (await states.SaveAsync()).Ok.ShouldBeTrue();
        await Expect(states.Unsaved).ToHaveCountAsync(0);

        await automations.OpenAsync("SHIP", "features");
        await Expect(automations.Rows).ToHaveCountAsync(1);
        await Expect(automations.Invalid).ToHaveCountAsync(1);

        await AddIdeaStateAsync(states);
        await automations.OpenAsync("SHIP", "features");
        await Expect(automations.Rows).ToHaveCountAsync(1);
        await Expect(automations.Invalid).ToHaveCountAsync(0);

        // 4. A story with no sprint that goes from Proposed to In progress lands in the current sprint.
        var story = await ApiAsync("POST", "/api/work-items", new { projectId, type = "story", title = "Start me" });
        (await QueryAsync<long>("SELECT count(*) FROM work_item WHERE id = @id AND sprint_id IS NULL", ("id", story.GetProperty("id").GetGuid()))).ShouldBe(1);
        var started = await ApiAsync("PATCH", $"/api/work-items/{story.GetProperty("id").GetGuid()}", new
        {
            expectedRevision = story.GetProperty("revision").GetInt32(),
            changes = new { state = new { value = SeedIds.States.Ship.StoryActive } },
        });
        started.GetProperty("notices").EnumerateArray().Select(n => n.GetString()).ShouldContain("sprinted");
        var sprintId = await QueryAsync<Guid>("SELECT sprint_id FROM work_item WHERE id = @id", ("id", story.GetProperty("id").GetGuid()));
        sprintId.ShouldBe(SeedIds.Sprints.Sprint24);
    }

    private static async Task<int> RowOfAsync(SettingsStatesPage states, string name)
    {
        var count = await states.Rows.CountAsync();
        for (var row = 0; row < count; row++)
        {
            if (await states.NameOf(row).InputValueAsync() == name)
            {
                return row;
            }
        }

        throw new InvalidOperationException($"No state row named {name}.");
    }

    private async Task<JsonElement> DeliveredContextAsync(Guid workItemId)
    {
        var json = await QueryAsync<string>(
            "SELECT d.body::text FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id WHERE s.work_item_id = @id AND d.action = 'created'",
            ("id", workItemId));
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
