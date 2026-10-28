using System.Net;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.LinearApi;

// The mutations, with the documents @linear/sdk sends verbatim (Fixtures/linear-sdk): each activity type and the state it
// leaves the session in, ephemeral replacement, Stop answered by a response, PR links, the agent's own comments, plan, external
// urls, another connection's session and the stale sweep. Each test works on a session of its own.
[TestFixture]
public class LinearApiMutationTests : ApiFixture
{
    private LinearApiWorld _world = null!;

    private static DateTimeOffset Now => TimeProvider.System.GetUtcNow();

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var database = await OpenDatabaseAsync();
        _world = await LinearApiWorld.CreateAsync(Api, database);
    }

    private async Task<JsonNode> OkAsync(string document, object variables, string? token = null)
    {
        var (status, body) = await LinearGraphQl.PostAsync(Client, token ?? _world.TokenA, LinearGraphQl.Document(document), variables);
        status.ShouldBe(HttpStatusCode.OK, body.ToJsonString());
        body["errors"].ShouldBeNull(body.ToJsonString());
        return body["data"]!;
    }

    private async Task<string?> ErrorCodeAsync(string document, object variables, string? token = null)
    {
        var (_, body) = await LinearGraphQl.PostAsync(Client, token ?? _world.TokenA, LinearGraphQl.Document(document), variables);
        return LinearGraphQl.ErrorCode(body);
    }

    private async Task<Guid> ActivityAsync(Guid session, object content, bool? ephemeral = null, string? signal = null)
    {
        var data = await OkAsync("createAgentActivity", new { input = new { agentSessionId = session, content, ephemeral, signal } });
        var payload = data["agentActivityCreate"]!;
        payload["success"]!.GetValue<bool>().ShouldBeTrue();
        payload["lastSyncId"].ShouldNotBeNull();
        return Guid.Parse(payload["agentActivity"]!["id"]!.GetValue<string>());
    }

    private static async Task<(string State, DateTimeOffset? EndedAt, DateTimeOffset LastActivity)> StateAsync(Npgsql.NpgsqlConnection database, Guid session)
        => await database.QuerySingleAsync<(string, DateTimeOffset?, DateTimeOffset)>(
            "SELECT state, ended_at, last_activity_at FROM agent_session WHERE id = @session", new { session });

    [TestCase("thought", "working")]
    [TestCase("action", "working")]
    [TestCase("elicitation", "inputRequired")]
    [TestCase("response", "completed")]
    [TestCase("error", "failed")]
    public async Task AgentActivityCreate_WhenTypeIs_ShouldMoveTheSessionToItsState(string type, string expected)
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database, "submitted", lastActivity: Now.AddMinutes(-5));
        object content = type == "action" ? new { type, action = "Run", parameter = "dotnet test", result = "passed" } : new { type, body = $"A {type}" };

        var id = await ActivityAsync(session, content);

        var state = await StateAsync(database, session);
        state.State.ShouldBe(expected);
        (state.EndedAt is not null).ShouldBe(expected is "completed" or "failed");
        state.LastActivity.ShouldBeGreaterThan(Now.AddMinutes(-1));
        var row = await database.QuerySingleAsync<(string, string?, string?)>("SELECT type, body, action FROM agent_activity WHERE id = @id", new { id });
        row.Item1.ShouldBe(type);
        (type == "action" ? row.Item3 : row.Item2).ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task AgentActivityCreate_WhenAnElicitationIsAnswered_ShouldWorkAgain()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);
        await ActivityAsync(session, new { type = "elicitation", body = "Which database?" }, signal: "select");

        (await StateAsync(database, session)).State.ShouldBe("inputRequired");
        await ActivityAsync(session, new { type = "thought", body = "Going with Postgres" });

        (await StateAsync(database, session)).State.ShouldBe("working");
        (await database.ExecuteScalarAsync<string>("SELECT signal FROM agent_activity WHERE session_id = @session AND type = 'elicitation'", new { session })).ShouldBe("select");
    }

    [Test]
    public async Task AgentActivityCreate_WhenAnEphemeralIsFollowedByAnotherActivity_ShouldReplaceIt()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);

        var first = await ActivityAsync(session, new { type = "thought", body = "Thinking..." }, ephemeral: true);
        var second = await ActivityAsync(session, new { type = "action", action = "Read", parameter = "file" }, ephemeral: true);
        var third = await ActivityAsync(session, new { type = "thought", body = "Done thinking" });

        var ids = (await database.QueryAsync<Guid>("SELECT id FROM agent_activity WHERE session_id = @session AND replaced_at IS NULL", new { session })).ToList();
        ids.ShouldBe([third]);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM agent_activity WHERE session_id = @session AND id IN (@first, @second) AND replaced_at IS NOT NULL", new { session, first, second })).ShouldBe(2);
        var read = await OkAsync("agentSession_activities", new { id = session });
        read["agentSession"]!["activities"]!["nodes"]!.AsArray().Select(n => n!["id"]!.GetValue<string>()).ShouldBe([third.ToString()]);
    }

    [TestCase("response")]
    [TestCase("elicitation")]
    [TestCase("error")]
    public async Task AgentActivityCreate_WhenEphemeralIsNotOnAThoughtOrAnAction_ShouldBeInvalid(string type)
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);

        var code = await ErrorCodeAsync("createAgentActivity", new { input = new { agentSessionId = session, content = new { type, body = "x" }, ephemeral = true } });

        code.ShouldBe("INVALID_INPUT");
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM agent_activity WHERE session_id = @session", new { session })).ShouldBe(0);
    }

    [Test]
    public async Task AgentActivityCreate_WhenTheAgentSendsAPrompt_ShouldBeInvalid()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);

        var code = await ErrorCodeAsync("createAgentActivity", new { input = new { agentSessionId = session, content = new { type = "prompt", body = "Do it" } } });

        code.ShouldBe("INVALID_INPUT");
        (await StateAsync(database, session)).State.ShouldBe("working");
    }

    [Test]
    public async Task AgentActivityCreate_WhenStopWasRequestedAndTheAgentResponds_ShouldCancelTheSession()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database, stopRequested: true);

        await ActivityAsync(session, new { type = "response", body = "Stopped as asked" });

        var state = await StateAsync(database, session);
        state.State.ShouldBe("canceled");
        state.EndedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task AgentActivityCreate_WhenStopWasRequestedAndTheAgentFails_ShouldFailTheSession()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database, stopRequested: true);

        await ActivityAsync(session, new { type = "error", body = "Could not stop cleanly" });

        (await StateAsync(database, session)).State.ShouldBe("failed");
    }

    [Test]
    public async Task AgentActivityCreate_WhenTheSessionHasEnded_ShouldKeepItsState()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);
        await ActivityAsync(session, new { type = "response", body = "Done" });

        await ActivityAsync(session, new { type = "thought", body = "One more thing" });

        (await StateAsync(database, session)).State.ShouldBe("completed");
    }

    [Test]
    public async Task AgentActivityCreate_WhenTheSessionWasStale_ShouldWorkAgain()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database, "stale", lastActivity: Now.AddHours(-2));

        await ActivityAsync(session, new { type = "thought", body = "Back" });

        (await StateAsync(database, session)).State.ShouldBe("working");
    }

    [Test]
    public async Task AgentActivityCreate_WhenResponding_ShouldPostACommentThatIsNeverPrompted()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, item, _) = await _world.AddSessionAsync(Api, database);
        await database.ExecuteAsync(
            "INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at) VALUES (gen_random_uuid(), @session, 1, 'created', '{}'::jsonb, now(), 'sent', now())",
            new { session });

        await ActivityAsync(session, new { type = "response", body = "All **done**" });

        var comment = await database.QuerySingleAsync<(string Body, string? AgentName, Guid? AuthorId)>(
            "SELECT body, agent_name, author_id FROM comment WHERE work_item_id = @item", new { item });
        comment.Body.ShouldBe("All **done**");
        comment.AgentName.ShouldBe(_world.NameA);
        comment.AuthorId.ShouldBe(ShipLean.TestSupport.SeedIds.Users.Joel);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM agent_delivery WHERE session_id = @session AND action = 'prompted'", new { session })).ShouldBe(0);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM agent_activity WHERE session_id = @session AND type = 'prompt'", new { session })).ShouldBe(0);
    }

    [Test]
    public async Task AgentActivityCreate_WhenFailing_ShouldPostTheErrorAsAComment()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, item, _) = await _world.AddSessionAsync(Api, database);

        await ActivityAsync(session, new { type = "error", body = "Build failed" });

        (await database.QuerySingleAsync<string>("SELECT agent_name FROM comment WHERE work_item_id = @item AND body = 'Build failed'", new { item })).ShouldBe(_world.NameA);
    }

    [Test]
    public async Task CommentCreate_WhenTheSdkDocumentRuns_ShouldPostAStampedCommentThatIsNeverPrompted()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, item, key) = await _world.AddSessionAsync(Api, database);

        var data = await OkAsync("createComment", new { input = new { issueId = key, body = "A note from the agent" } });

        var payload = data["commentCreate"]!;
        payload["success"]!.GetValue<bool>().ShouldBeTrue();
        var id = Guid.Parse(payload["comment"]!["id"]!.GetValue<string>());
        (await database.QuerySingleAsync<string>("SELECT agent_name FROM comment WHERE id = @id", new { id })).ShouldBe(_world.NameA);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM agent_delivery WHERE session_id = @session AND action = 'prompted'", new { session })).ShouldBe(0);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM comment WHERE work_item_id = @item", new { item })).ShouldBe(1);
    }

    [Test]
    public async Task CommentCreate_WhenTheItemIsAnotherConnections_ShouldBeNotFound()
    {
        var code = await ErrorCodeAsync("createComment", new { input = new { issueId = _world.KeyB, body = "Sneaky" } });

        code.ShouldBe("NOT_FOUND");
    }

    [Test]
    public async Task AgentSessionUpdate_WhenAPullRequestUrlIsAdded_ShouldLinkItToTheItem()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, item, _) = await _world.AddSessionAsync(Api, database);
        var variables = new { id = session, input = new { externalUrls = new[] { new { label = "Pull request", url = "https://github.com/ShipLean/ShipLean/pull/77" } } } };

        var data = await OkAsync("updateAgentSession", variables);
        await OkAsync("updateAgentSession", variables);

        data["agentSessionUpdate"]!["agentSession"]!["id"]!.GetValue<string>().ShouldBe(session.ToString());
        var links = (await database.QueryAsync<string>("SELECT url FROM work_item_link WHERE source_id = @item AND link_type = 'url'", new { item })).ToList();
        links.ShouldBe(["https://github.com/ShipLean/ShipLean/pull/77"]);
    }

    [Test]
    public async Task AgentSessionUpdate_WhenAGitLabMergeRequestUrlIsAdded_ShouldLinkItToTheItem()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, item, _) = await _world.AddSessionAsync(Api, database);

        await OkAsync("agentSessionUpdateExternalUrl", new { id = session, input = new { addedExternalUrls = new[] { new { label = "MR", url = "https://gitlab.com/group/sub/repo/-/merge_requests/7" } } } });

        (await database.QuerySingleAsync<string>("SELECT url FROM work_item_link WHERE source_id = @item AND link_type = 'url'", new { item })).ShouldBe("https://gitlab.com/group/sub/repo/-/merge_requests/7");
    }

    [Test]
    public async Task AgentSessionUpdate_WhenTheUrlIsNotAPullRequest_ShouldKeepItOnTheSessionOnly()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, item, _) = await _world.AddSessionAsync(Api, database);

        await OkAsync("updateAgentSession", new { id = session, input = new { addedExternalUrls = new[] { new { label = "Log", url = "https://example.com/run/1" } } } });

        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM work_item_link WHERE source_id = @item AND link_type = 'url'", new { item })).ShouldBe(0);
        (await database.ExecuteScalarAsync<string>("SELECT external_urls->0->>'url' FROM agent_session WHERE id = @session", new { session })).ShouldBe("https://example.com/run/1");
    }

    [Test]
    public async Task AgentSessionUpdate_WhenExternalUrlsAreAddedRemovedAndReplaced_ShouldApplyEachInTurn()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);
        async Task<string> UrlsAsync() => await database.ExecuteScalarAsync<string>("SELECT external_urls::text FROM agent_session WHERE id = @session", new { session }) ?? string.Empty;

        await OkAsync("updateAgentSession", new { id = session, input = new { addedExternalUrls = new[] { new { label = "A", url = "https://example.com/a" }, new { label = "B", url = "https://example.com/b" } } } });
        (await UrlsAsync()).ShouldContain("example.com/a");
        (await UrlsAsync()).ShouldContain("example.com/b");

        await OkAsync("updateAgentSession", new { id = session, input = new { removedExternalUrls = new[] { "https://example.com/a" } } });
        (await UrlsAsync()).ShouldNotContain("example.com/a");
        (await UrlsAsync()).ShouldContain("example.com/b");

        await OkAsync("agentSessionUpdateExternalUrl", new { id = session, input = new { externalUrls = new[] { new { label = "C", url = "https://example.com/c" } } } });
        var replaced = await UrlsAsync();
        replaced.ShouldContain("example.com/c");
        replaced.ShouldNotContain("example.com/b");

        await OkAsync("updateAgentSession", new { id = session, input = new { addedExternalUrls = new[] { new { label = "C again", url = "https://example.com/c" } } } });
        (await database.ExecuteScalarAsync<string>("SELECT external_urls->0->>'label' FROM agent_session WHERE id = @session", new { session })).ShouldBe("C again");
    }

    [Test]
    public async Task AgentSessionUpdate_WhenThePlanIsSentTwice_ShouldReplaceItInFull()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);
        var first = new[] { new { content = "Write it", status = "pending" }, new { content = "Test it", status = "pending" } };
        var second = new[] { new { content = "Ship it", status = "inProgress" } };

        await OkAsync("updateAgentSession", new { id = session, input = new { plan = first } });
        await OkAsync("updateAgentSession", new { id = session, input = new { plan = second } });

        var plan = JsonNode.Parse((await database.ExecuteScalarAsync<string>("SELECT plan::text FROM agent_session WHERE id = @session", new { session }))!)!.AsArray();
        plan.Count.ShouldBe(1);
        plan[0]!["content"]!.GetValue<string>().ShouldBe("Ship it");
        plan[0]!["status"]!.GetValue<string>().ShouldBe("inProgress");
    }

    [Test]
    public async Task AgentSessionUpdate_WhenThePlanStatusIsUnknown_ShouldBeInvalid()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);

        var code = await ErrorCodeAsync("updateAgentSession", new { id = session, input = new { plan = new[] { new { content = "Do it", status = "someday" } } } });

        code.ShouldBe("INVALID_INPUT");
    }

    [Test]
    public async Task AgentSessionUpdate_WhenTheUrlIsNotWeb_ShouldBeInvalid()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database);

        var code = await ErrorCodeAsync("updateAgentSession", new { id = session, input = new { addedExternalUrls = new[] { new { label = "x", url = "javascript:alert(1)" } } } });

        code.ShouldBe("INVALID_INPUT");
    }

    [Test]
    public async Task Mutations_WhenTheSessionIsAnotherConnections_ShouldBeNotFoundAndChangeNothing()
    {
        await using var database = await OpenDatabaseAsync();

        (await ErrorCodeAsync("createAgentActivity", new { input = new { agentSessionId = _world.SessionB, content = new { type = "thought", body = "Mine now" } } })).ShouldBe("NOT_FOUND");
        (await ErrorCodeAsync("updateAgentSession", new { id = _world.SessionB, input = new { plan = new[] { new { content = "x", status = "pending" } } } })).ShouldBe("NOT_FOUND");
        (await ErrorCodeAsync("agentSessionUpdateExternalUrl", new { id = _world.SessionB, input = new { externalUrls = new[] { new { label = "x", url = "https://example.com" } } } })).ShouldBe("NOT_FOUND");
        (await ErrorCodeAsync("createAgentActivity", new { input = new { agentSessionId = Guid.NewGuid(), content = new { type = "thought", body = "Nobody's" } } })).ShouldBe("NOT_FOUND");

        (await StateAsync(database, _world.SessionB)).State.ShouldBe("submitted");
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM agent_activity WHERE session_id = @id", new { id = _world.SessionB })).ShouldBe(0);
    }

    [Test]
    public async Task Sweep_WhenAWorkingSessionIsSilentForThirtyMinutes_ShouldMarkItStaleOnce()
    {
        await using var database = await OpenDatabaseAsync();
        var (silent, _, _) = await _world.AddSessionAsync(Api, database, lastActivity: Now.AddMinutes(-31));
        var (recent, _, _) = await _world.AddSessionAsync(Api, database, lastActivity: Now.AddMinutes(-29));
        var (waiting, _, _) = await _world.AddSessionAsync(Api, database, "inputRequired", lastActivity: Now.AddHours(-3));
        var sweeper = Host.Services.GetRequiredService<AgentStaleSweeper>();

        var first = await sweeper.SweepAsync(CancellationToken.None);
        var second = await sweeper.SweepAsync(CancellationToken.None);

        first.ShouldBeGreaterThanOrEqualTo(1);
        second.ShouldBe(0);
        (await StateAsync(database, silent)).State.ShouldBe("stale");
        (await StateAsync(database, recent)).State.ShouldBe("working");
        (await StateAsync(database, waiting)).State.ShouldBe("inputRequired");
    }

    [Test]
    public async Task Sweep_WhenTheAgentSpokeMeanwhile_ShouldLeaveItWorking()
    {
        await using var database = await OpenDatabaseAsync();
        var (session, _, _) = await _world.AddSessionAsync(Api, database, lastActivity: Now.AddMinutes(-40));
        await ActivityAsync(session, new { type = "thought", body = "Still here" });

        await Host.Services.GetRequiredService<AgentStaleSweeper>().SweepAsync(CancellationToken.None);

        (await StateAsync(database, session)).State.ShouldBe("working");
    }
}
