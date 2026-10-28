using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// GET api/work-items/{id}/agent-sessions: the Agent panel's one request. Each test arranges a project of its own.
[TestFixture]
public class AgentSessionsReadTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<AgentPanelDto> PanelAsync(Guid itemId)
    {
        using var response = await Api.GetAsync($"api/work-items/{itemId}/agent-sessions");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<AgentPanelDto>(text, Wire)!;
    }

    private static async Task<Guid> InsertSessionAsync(
        NpgsqlConnection database, AgentDispatchArrange arranged, Guid itemId, string state, DateTimeOffset createdAt, string plan = "[]", string urls = "[]")
    {
        var id = Guid.CreateVersion7();
        await database.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, external_urls, plan, last_activity_at, created_by, created_at, ended_at)
            VALUES (@id, @itemId, @connectionId, @state, 'https://github.com/ShipLean/ShipLean.git', 'develop', @urls::jsonb, @plan::jsonb, @createdAt,
                    (SELECT id FROM app_user LIMIT 1), @createdAt, CASE WHEN @state IN ('completed', 'failed', 'canceled') THEN @createdAt END)
            """,
            new { id, itemId, connectionId = arranged.ConnectionId, state, createdAt, plan, urls });
        return id;
    }

    private static Task InsertActivityAsync(NpgsqlConnection database, Guid sessionId, string type, string? body, bool ephemeral, DateTimeOffset at)
        => database.ExecuteAsync(
            "INSERT INTO agent_activity (id, session_id, type, body, ephemeral, created_at) VALUES (@id, @sessionId, @type, @body, @ephemeral, @at)",
            new { id = Guid.CreateVersion7(), sessionId, type, body, ephemeral, at });

    [Test]
    public async Task Sessions_WhenProjectHasNoConnectionAndNoSession_ShouldBeHidden()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api, connection: false);
        var item = await arranged.CreateItemAsync(Api);

        var panel = await PanelAsync(item.Id);

        panel.Visible.ShouldBeFalse();
        panel.Sessions.ShouldBeEmpty();
        panel.Availability.Reason.ShouldBe(AgentStartBlock.NoConnection);
    }

    [Test]
    public async Task Sessions_WhenProjectHasAConnectionButNoSession_ShouldShowStartAvailability()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);

        var panel = await PanelAsync(item.Id);

        panel.Visible.ShouldBeTrue();
        panel.Sessions.ShouldBeEmpty();
        panel.Availability.CanStart.ShouldBeTrue();
    }

    [Test]
    public async Task Sessions_WhenConnectionIsMissingButAnEarlierSessionExists_ShouldStayVisibleWithTheReason()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        await InsertSessionAsync(database, arranged, item.Id, "completed", TimeProvider.System.GetUtcNow().AddHours(-3));
        await database.ExecuteAsync("UPDATE project SET agent_connection_id = NULL WHERE id = @id", new { id = arranged.Project.Id });

        var panel = await PanelAsync(item.Id);

        panel.Visible.ShouldBeTrue();
        panel.Sessions.ShouldHaveSingleItem();
        panel.Availability.Reason.ShouldBe(AgentStartBlock.NoConnection);
        panel.Availability.Message.ShouldNotBeEmpty();
    }

    [Test]
    public async Task Sessions_ShouldListNewestFirstWithConnectionNameStateLabelsLinksAndPlan()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var now = TimeProvider.System.GetUtcNow();
        var old = await InsertSessionAsync(database, arranged, item.Id, "failed", now.AddDays(-2));
        var current = await InsertSessionAsync(
            database, arranged, item.Id, "working", now.AddMinutes(-5),
            plan: """[{"content":"Write the test","status":"completed"},{"content":"Fix it","status":"inProgress"}]""",
            urls: """[{"label":"Pull request","url":"https://github.com/ShipLean/ShipLean/pull/9"}]""");

        var panel = await PanelAsync(item.Id);

        panel.Sessions.Select(s => s.Id).ShouldBe([current, old]);
        var first = panel.Sessions[0];
        first.State.ShouldBe("working");
        first.Live.ShouldBeTrue();
        first.StopRequested.ShouldBeFalse();
        first.ConnectionName.ShouldStartWith("Relay ");
        first.StartedLabel.ShouldBe(ShipLean.Core.DisplayDate.Relative(first.StartedAt, TimeProvider.System));
        first.EndedLabel.ShouldBeNull();
        first.Urls.ShouldBe([new AgentUrlDto("Pull request", "https://github.com/ShipLean/ShipLean/pull/9")]);
        first.Plan.ShouldBe([new AgentPlanStepDto("Write the test", "completed"), new AgentPlanStepDto("Fix it", "inProgress")]);
        panel.Sessions[1].Live.ShouldBeFalse();
        panel.Sessions[1].EndedLabel.ShouldNotBeNull();
        panel.Availability.CanStart.ShouldBeFalse();
        panel.Availability.Reason.ShouldBe(AgentStartBlock.LiveSession);
    }

    [Test]
    public async Task Sessions_WhenTheNewestActivityIsEphemeral_ShouldKeepOnlyThatEphemeralOne()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var at = TimeProvider.System.GetUtcNow().AddMinutes(-10);
        var session = await InsertSessionAsync(database, arranged, item.Id, "working", at);
        await InsertActivityAsync(database, session, "thought", "first thought", false, at.AddSeconds(1));
        await InsertActivityAsync(database, session, "action", "Searching", true, at.AddSeconds(2));
        await InsertActivityAsync(database, session, "action", "Reading", true, at.AddSeconds(3));
        await InsertActivityAsync(database, session, "elicitation", "Which branch?", false, at.AddSeconds(4));
        await InsertActivityAsync(database, session, "action", "Waiting", true, at.AddSeconds(5));

        var activities = (await PanelAsync(item.Id)).Sessions.Single().Activities;

        activities.Select(a => a.Body).ShouldBe(["first thought", "Which branch?", "Waiting"]);
        activities.Select(a => a.Ephemeral).ShouldBe([false, false, true]);
    }

    [Test]
    public async Task Sessions_WhenTheNewestActivityIsNotEphemeral_ShouldDropEveryEphemeralOne()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var at = TimeProvider.System.GetUtcNow().AddMinutes(-10);
        var session = await InsertSessionAsync(database, arranged, item.Id, "working", at);
        await InsertActivityAsync(database, session, "action", "Searching", true, at.AddSeconds(1));
        await InsertActivityAsync(database, session, "thought", "Found it", false, at.AddSeconds(2));

        var activities = (await PanelAsync(item.Id)).Sessions.Single().Activities;

        activities.Select(a => a.Body).ShouldBe(["Found it"]);
    }

    [Test]
    public async Task Sessions_WhenAnotherSessionHasEphemeralActivity_ShouldCollapseEachSessionOnItsOwn()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var at = TimeProvider.System.GetUtcNow().AddHours(-5);
        var earlier = await InsertSessionAsync(database, arranged, item.Id, "completed", at);
        await InsertActivityAsync(database, earlier, "action", "Old status", true, at.AddSeconds(1));
        var later = await InsertSessionAsync(database, arranged, item.Id, "working", at.AddHours(1));
        await InsertActivityAsync(database, later, "thought", "Now", false, at.AddHours(1).AddSeconds(1));

        var sessions = (await PanelAsync(item.Id)).Sessions;

        sessions[0].Activities.Select(a => a.Body).ShouldBe(["Now"]);
        sessions[1].Activities.Select(a => a.Body).ShouldBe(["Old status"]);
    }

    [Test]
    public async Task Sessions_WhenTheItemDoesNotExist_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync($"api/work-items/{Guid.NewGuid()}/agent-sessions");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Start_WhenASessionOpens_ShouldPublishAgentSessionChangedForTheItem()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), arranged.Project.Id);

        using var response = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var changed = await watch.NextAsync<AgentSessionChanged>(e => e.WorkItemId == item.Id);
        changed.ProjectId.ShouldBe(arranged.Project.Id);
        changed.SessionId.ShouldBe((await AgentDispatchArrange.SessionsAsync(database, item.Id)).Single().Id);
    }

    [Test]
    public async Task Stop_WhenASessionIsLive_ShouldPublishAgentSessionChanged()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var started = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), arranged.Project.Id);

        using var stopped = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session/stop");
        stopped.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await watch.NextAsync<AgentSessionChanged>(e => e.WorkItemId == item.Id)).ShouldNotBeNull();
        var panel = await PanelAsync(item.Id);
        panel.Sessions.Single().StopRequested.ShouldBeTrue();
        panel.Sessions.Single().Activities.Select(a => (a.Type, a.Signal)).ShouldBe([("prompt", "stop")]);
    }
}
