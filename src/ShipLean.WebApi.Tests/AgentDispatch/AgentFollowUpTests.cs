using System.Net;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.WorkItemDetails.Comments;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Boards.Settings;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// A person's comment and Stop on an item with a live session: what is queued, in what order, and what is not.
[TestFixture]
public class AgentFollowUpTests : ApiFixture
{
    private async Task<(Guid ItemId, Guid SessionId, AgentDispatchArrange Arranged)> OpenSessionAsync(NpgsqlConnection database)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var started = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK, await started.Content.ReadAsStringAsync());
        return (item.Id, (await AgentDispatchArrange.SessionsAsync(database, item.Id)).Single().Id, arranged);
    }

    private async Task<HttpResponseMessage> CommentAsync(Guid itemId, string text)
        => await Api.PostJsonAsync($"api/work-items/{itemId}/comments", new { body = text });

    [Test]
    public async Task Comment_WhenAPersonCommentsOnAnItemWithALiveSession_ShouldQueuePromptedWithTheTextAndAPromptActivity()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, sessionId, _) = await OpenSessionAsync(database);

        using var response = await CommentAsync(itemId, "Please also cover **empty** input");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var commentId = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();
        var deliveries = await AgentDispatchArrange.DeliveriesAsync(database, itemId);
        deliveries.Select(d => (d.Seq, d.Action, d.Status)).ShouldBe([(1, "created", "pending"), (2, "prompted", "pending")]);
        var body = AgentDispatchArrange.BodyOf(deliveries[1]);
        body["action"]!.GetValue<string>().ShouldBe("prompted");
        body["agentSession"]!["id"]!.GetValue<string>().ShouldBe(sessionId.ToString());
        body["agentSession"]!["status"]!.GetValue<string>().ShouldBe("pending");
        body["agentActivity"]!["content"]!["type"]!.GetValue<string>().ShouldBe("prompt");
        body["agentActivity"]!["content"]!["body"]!.GetValue<string>().ShouldBe("Please also cover **empty** input");
        body["agentActivity"]!["sourceCommentId"]!.GetValue<string>().ShouldBe(commentId);
        body["agentActivity"]!["signal"].ShouldBeNull();
        var activity = (await database.QueryAsync<(string Type, string? Body, string? Signal)>(
            "SELECT type, body, signal FROM agent_activity WHERE session_id = @sessionId", new { sessionId })).ShouldHaveSingleItem();
        activity.ShouldBe(("prompt", "Please also cover **empty** input", null));
    }

    [Test]
    public async Task Comment_WhenTwoPeopleCommentInARow_ShouldQueueThemInOrder()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, _, _) = await OpenSessionAsync(database);

        (await CommentAsync(itemId, "First")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CommentAsync(itemId, "Second")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var bodies = (await AgentDispatchArrange.DeliveriesAsync(database, itemId)).Skip(1).Select(d => (d.Seq, AgentDispatchArrange.BodyOf(d)["agentActivity"]!["content"]!["body"]!.GetValue<string>()));
        bodies.ShouldBe([(2, "First"), (3, "Second")]);
    }

    [Test]
    public async Task Comment_WhenTheItemHasNoSession_ShouldQueueNothing()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);

        (await CommentAsync(item.Id, "Hello")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Comment_WhenTheSessionHasEnded_ShouldQueueNothing()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, sessionId, _) = await OpenSessionAsync(database);
        await database.ExecuteAsync("UPDATE agent_session SET state = 'completed', ended_at = now() WHERE id = @sessionId", new { sessionId });

        (await CommentAsync(itemId, "Thanks")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await AgentDispatchArrange.DeliveriesAsync(database, itemId)).ShouldHaveSingleItem().Action.ShouldBe("created");
    }

    [Test]
    public async Task Comment_WhenWrittenAsAnAgent_ShouldNeverQueuePrompted()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, sessionId, _) = await OpenSessionAsync(database);
        await using var scope = Host.Services.CreateAsyncScope();
        var comments = scope.ServiceProvider.GetRequiredService<ICommentStore>();

        var written = await comments.AddAsAgentAsync(itemId, new CommentCreate("All done, PR is up"), "Relay", SeedIds.Users.Joel);

        written.AgentName.ShouldBe("Relay");
        (await AgentDispatchArrange.DeliveriesAsync(database, itemId)).ShouldHaveSingleItem().Action.ShouldBe("created");
        (await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM agent_activity WHERE session_id = @sessionId", new { sessionId })).ShouldBe(0);
    }

    [Test]
    public async Task Comment_WhenAnMcpClientWithNoNameWritesIt_ShouldNeverQueuePrompted()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, sessionId, _) = await OpenSessionAsync(database);
        await using var scope = Host.Services.CreateAsyncScope();
        // The store is what every MCP tool writes through; an unnamed client carries no agent stamp, so only the endpoint's hook can tell a person.
        var store = new CommentStore(
            scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>(), new FixedCurrentUser(SeedIds.Users.Joel), AgentIdentity.None,
            scope.ServiceProvider.GetRequiredService<MarkdownRenderer>(), TimeProvider.System, new DiscardingProjectEvents());

        var written = await store.AddAsync(itemId, new CommentCreate("From an MCP client that did not give its name"));

        written.AgentName.ShouldBeNull();
        (await AgentDispatchArrange.DeliveriesAsync(database, itemId)).ShouldHaveSingleItem().Action.ShouldBe("created");
        (await database.ExecuteScalarAsync<int>("SELECT count(*)::int FROM agent_activity WHERE session_id = @sessionId", new { sessionId })).ShouldBe(0);
    }

    [Test]
    public async Task Stop_WhenTheSessionIsLive_ShouldQueuePromptedWithTheStopSignalAndKeepTheSessionLive()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, sessionId, _) = await OpenSessionAsync(database);

        using var response = await Api.PostJsonAsync($"api/work-items/{itemId}/agent-session/stop");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var deliveries = await AgentDispatchArrange.DeliveriesAsync(database, itemId);
        deliveries.Select(d => (d.Seq, d.Action)).ShouldBe([(1, "created"), (2, "prompted")]);
        var activity = AgentDispatchArrange.BodyOf(deliveries[1])["agentActivity"]!;
        activity["signal"]!.GetValue<string>().ShouldBe("stop");
        activity["content"]!["body"]!.GetValue<string>().ShouldBeEmpty();
        var session = await database.QuerySingleAsync<(string State, DateTimeOffset? StopRequestedAt)>(
            "SELECT state, stop_requested_at FROM agent_session WHERE id = @sessionId", new { sessionId });
        session.State.ShouldBe("submitted");
        session.StopRequestedAt.ShouldNotBeNull();
        (await database.ExecuteScalarAsync<string>("SELECT signal FROM agent_activity WHERE session_id = @sessionId", new { sessionId })).ShouldBe("stop");
    }

    [Test]
    public async Task Stop_WhenPressedTwice_ShouldQueueOnlyOne()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, _, _) = await OpenSessionAsync(database);

        (await Api.PostJsonAsync($"api/work-items/{itemId}/agent-session/stop")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Api.PostJsonAsync($"api/work-items/{itemId}/agent-session/stop")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await AgentDispatchArrange.DeliveriesAsync(database, itemId)).Count(d => d.Action == "prompted").ShouldBe(1);
    }

    [Test]
    public async Task Stop_WhenNoSessionIsLive_ShouldBeConflict()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);

        using var response = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session/stop");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Stop_WhenTheCallerIsNotAMember_ShouldBeNotFound()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, _, _) = await OpenSessionAsync(database);

        using var response = await Api.ActingAs(TestUser.Mei).PostJsonAsync($"api/work-items/{itemId}/agent-session/stop");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AgentDispatchArrange.DeliveriesAsync(database, itemId)).ShouldHaveSingleItem();
    }
}
