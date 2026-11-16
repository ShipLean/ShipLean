using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Secrets;
using ShipLean.WebApi.Features.Mcp.Transport;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Agents.GetAgentSession;

// get_agent_session over /mcp, with real rows. Joel is an admin of SHIP and Priya a member; Joel is not in PART. Every test arranges
// its own item, so its sessions are the only ones it reads.
[TestFixture]
public class GetAgentSessionTests : McpFixture
{
    private const string Detail = "relay-host.internal:8443 refused the connection";
    private const string AttemptError = "HttpRequestException: Connection refused (relay-host.internal:8443)";

    protected override bool WithProbeTools => false;

    private DateTimeOffset Now => Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();

    [Test]
    public async Task GetAgentSession_WhenCallerIsAdminOnADeliveryFailedSession_ShouldReturnReasonDetailAttemptsAndRunLog()
    {
        var (key, _) = await ArrangeDeliveryFailedAsync();

        var reply = await CallAsync(TestUser.Joel, $$"""{"item":"{{key}}"}""");

        var session = reply.Structured().GetProperty("session");
        session.GetProperty("end_reason").GetString().ShouldBe(AgentEndReasons.DeliveryFailed);
        session.GetProperty("end_detail").GetString().ShouldBe(Detail);
        session.GetProperty("end_text").GetString().ShouldBe(AgentEndText.For(AgentEndReasons.DeliveryFailed, Detail));
        session.GetProperty("run_log").GetProperty("attachment").GetString().ShouldStartWith("agent-run-");
        session.GetProperty("run_log").GetProperty("size_bytes").GetInt64().ShouldBe(2048);
        var deliveries = reply.Structured().GetProperty("deliveries").EnumerateArray().ToList();
        deliveries.Select(d => d.GetProperty("attempt").GetInt32()).ShouldBe([1, 2], "attempts are oldest first");
        deliveries[0].GetProperty("action").GetString().ShouldBe("created");
        deliveries[0].GetProperty("error").GetString().ShouldBe(AttemptError);
        deliveries[0].GetProperty("duration_ms").GetInt32().ShouldBe(5000);
        deliveries[1].GetProperty("http_status").GetInt32().ShouldBe(503);
    }

    [Test]
    public async Task GetAgentSession_WhenCallerIsMemberOnADeliveryFailedSession_ShouldHideDetailRunLogAndDeliveries()
    {
        var (key, _) = await ArrangeDeliveryFailedAsync();

        var reply = await CallAsync(TestUser.Priya, $$"""{"item":"{{key}}"}""");

        var session = reply.Structured().GetProperty("session");
        session.GetProperty("end_reason").GetString().ShouldBe(AgentEndReasons.DeliveryFailed);
        session.GetProperty("end_text").GetString().ShouldBe(AgentEndText.For(AgentEndReasons.DeliveryFailed, null));
        session.TryGetProperty("end_detail", out _).ShouldBeFalse();
        session.TryGetProperty("run_log", out _).ShouldBeFalse();
        reply.Structured().GetProperty("deliveries").GetArrayLength().ShouldBe(0);
        reply.Texts().ShouldAllBe(text => !text.Contains(Detail) && !text.Contains("relay-host.internal"));
    }

    [TestCase("submitted", "Joel")]
    [TestCase("working", "Joel")]
    [TestCase("inputRequired", "Joel")]
    [TestCase("submitted", "Priya")]
    [TestCase("working", "Priya")]
    [TestCase("inputRequired", "Priya")]
    public async Task GetAgentSession_WhenSessionIsLive_ShouldHaveNoEndFieldsAndNoDeliveriesForAnyRole(string state, string who)
    {
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
            key = await KeyAsync(connection, item);
            var relay = await AgentSessionSeeds.InsertConnectionAsync(connection);
            var session = await new AgentSessionSeeds(item, relay).WithState(state).InsertAsync(connection);
            await InsertAttemptsAsync(connection, session, relay);
        }

        var reply = await CallAsync(who == "Joel" ? TestUser.Joel : TestUser.Priya, $$"""{"item":"{{key}}"}""");

        var seen = reply.Structured().GetProperty("session");
        seen.GetProperty("state").GetString().ShouldBe(state);
        foreach (var field in new[] { "end_reason", "end_text", "end_detail", "run_log", "ended" })
        {
            seen.TryGetProperty(field, out _).ShouldBeFalse($"{field} of a live session");
        }

        reply.Structured().GetProperty("deliveries").GetArrayLength().ShouldBe(0);
        reply.Texts().ShouldAllBe(text => !text.Contains("relay-host.internal"));
    }

    [Test]
    public async Task GetAgentSession_WhenThereAre120Activities_ShouldPageNewestFirstInFiftyFiftyTwentyThenNoCursor()
    {
        var (key, session) = await ArrangeSessionWithActivitiesAsync(120);

        var first = await PageAsync($$"""{"session":"{{session}}"}""");
        var second = await PageAsync($$"""{"session":"{{session}}","before":"{{first.Next}}"}""");
        var third = await PageAsync($$"""{"item":"{{key}}","before":"{{second.Next}}"}""");

        first.Bodies.ShouldBe(Enumerable.Range(71, 50).Reverse().Select(i => $"activity {i}"), "the newest 50 first");
        second.Bodies.ShouldBe(Enumerable.Range(21, 50).Reverse().Select(i => $"activity {i}"));
        third.Bodies.ShouldBe(Enumerable.Range(1, 20).Reverse().Select(i => $"activity {i}"));
        first.Next.ShouldNotBeNull();
        second.Next.ShouldNotBeNull();
        third.Next.ShouldBeNull("the last page has no cursor");
    }

    [Test]
    public async Task GetAgentSession_WhenActivitiesFillExactlyOnePage_ShouldHaveNoCursor()
    {
        var (_, session) = await ArrangeSessionWithActivitiesAsync(10);

        var page = await PageAsync($$"""{"session":"{{session}}","limit":10}""");

        page.Bodies.Count.ShouldBe(10);
        page.Next.ShouldBeNull();
    }

    [Test]
    public async Task GetAgentSession_WhenActivitiesShareATimestamp_ShouldNotSkipOrRepeatAcrossPages()
    {
        Guid session;
        await using (var connection = await OpenDatabaseAsync())
        {
            var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
            session = await new AgentSessionSeeds(item, await AgentSessionSeeds.InsertConnectionAsync(connection)).InsertAsync(connection);
            await connection.ExecuteAsync(
                "INSERT INTO agent_activity (id, session_id, type, body, created_at) SELECT gen_random_uuid(), @session, 'thought', 'same ' || n, @at FROM generate_series(1, 7) n",
                new { session, at = Now.AddMinutes(-5) });
        }

        var first = await PageAsync($$"""{"session":"{{session}}","limit":3}""");
        var second = await PageAsync($$"""{"session":"{{session}}","limit":3,"before":"{{first.Next}}"}""");
        var third = await PageAsync($$"""{"session":"{{session}}","limit":3,"before":"{{second.Next}}"}""");

        first.Bodies.Concat(second.Bodies).Concat(third.Bodies).Order().ShouldBe(Enumerable.Range(1, 7).Select(n => $"same {n}"));
    }

    [Test]
    public async Task GetAgentSession_WhenBeforeIsNotACursor_ShouldBeAValidationErrorNamingBefore()
    {
        var (_, session) = await ArrangeSessionWithActivitiesAsync(1);

        var reply = await CallAsync(TestUser.Joel, $$"""{"session":"{{session}}","before":"yesterday"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("before");
    }

    [Test]
    public async Task GetAgentSession_WhenABodyIs10KB_ShouldCapItAt4KBAndFlagTruncated()
    {
        var (_, session) = await ArrangeSessionWithBodyAsync(new string('x', 10 * 1024));

        var reply = await CallAsync(TestUser.Joel, $$"""{"session":"{{session}}"}""");

        var activity = reply.Structured().GetProperty("activities")[0];
        activity.GetProperty("body").GetString()!.Length.ShouldBe(4096);
        activity.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task GetAgentSession_WhenABodyIsShort_ShouldNotFlagTruncated()
    {
        var (_, session) = await ArrangeSessionWithBodyAsync("short");

        var reply = await CallAsync(TestUser.Joel, $$"""{"session":"{{session}}"}""");

        var activity = reply.Structured().GetProperty("activities")[0];
        activity.GetProperty("body").GetString().ShouldBe("short");
        activity.GetProperty("truncated").GetBoolean().ShouldBeFalse();
    }

    // 3-byte characters: 4096 bytes ends inside the 1366th, so a cut by bytes leaves a broken character; 1365 whole ones are 4095 bytes.
    [Test]
    public async Task GetAgentSession_WhenABodyIsMultiByte_ShouldCutOnACharacterBoundary()
    {
        var (_, session) = await ArrangeSessionWithBodyAsync(new string('€', 2000));

        var reply = await CallAsync(TestUser.Joel, $$"""{"session":"{{session}}"}""");

        var activity = reply.Structured().GetProperty("activities")[0];
        activity.GetProperty("body").GetString().ShouldBe(new string('€', 1365));
        activity.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }

    // "a" then 4-byte characters (a surrogate pair each): 4096 bytes ends inside the 1024th, so a cut by bytes or by UTF-16 units splits one.
    [Test]
    public async Task GetAgentSession_WhenABodyHoldsSurrogatePairs_ShouldNotSplitOne()
    {
        var (_, session) = await ArrangeSessionWithBodyAsync("a" + string.Concat(Enumerable.Repeat("😀", 2000)));

        var reply = await CallAsync(TestUser.Joel, $$"""{"session":"{{session}}"}""");

        var body = reply.Structured().GetProperty("activities")[0].GetProperty("body").GetString()!;
        body.ShouldBe("a" + string.Concat(Enumerable.Repeat("😀", 1023)));
        Encoding.UTF8.GetByteCount(body).ShouldBeLessThanOrEqualTo(4096);
    }

    [Test]
    public async Task GetAgentSession_WhenItemHasNoSessions_ShouldBeNotFoundSayingSo()
    {
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            key = await KeyAsync(connection, await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection));
        }

        var reply = await CallAsync(TestUser.Joel, $$"""{"item":"{{key}}"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("not_found");
        error.GetProperty("message").GetString().ShouldContain("has no agent sessions");
    }

    [Test]
    public async Task GetAgentSession_WhenTheDeliveryBodyHoldsAToken_ShouldNeverReturnIt()
    {
        const string token = "shl_up_Zk3Fq8Lw0xYb7Nc2Vd9Ta5Rm1Hs4Jg6E";
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
            key = await KeyAsync(connection, item);
            var relay = await AgentSessionSeeds.InsertConnectionAsync(connection);
            var session = await new AgentSessionSeeds(item, relay).WithState("failed").WithEndReason(AgentEndReasons.AgentError).WithEndDetail("It broke.").InsertAsync(connection);
            var delivery = Guid.CreateVersion7();
            await connection.ExecuteAsync(
                "INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at) VALUES (@delivery, @session, 1, 'created', @body::jsonb, now(), 'failed', now())",
                new { delivery, session, body = $$"""{"uploadUrl":"https://shiplean.example/api/uploads/agent-log?token={{token}}","agentSession":{"prompt":"{{token}}"}}""" });
            await AgentDeliveryAttemptRows.InsertAsync(connection, new AgentDeliveryAttemptCreate(delivery, relay, 1, Now.AddMinutes(-1), 503, "Service Unavailable", 120, null));
        }

        var reply = await CallAsync(TestUser.Joel, $$"""{"item":"{{key}}"}""");

        reply.Structured().GetProperty("deliveries").GetArrayLength().ShouldBe(1);
        var leak = new SecretLeakAssert(token);
        leak.NotInJson(reply.Body);
        reply.Texts().ToList().ForEach(leak.NotInJson);
    }

    [TestCase(0)]
    [TestCase(101)]
    public async Task GetAgentSession_WhenLimitIsOutOfRangeForAMember_ShouldBeAValidationErrorNamingLimit(int limit)
    {
        var (key, _) = await ArrangeDeliveryFailedAsync();

        var reply = await CallAsync(TestUser.Priya, $$"""{"item":"{{key}}","limit":{{limit}}}""");

        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("limit");
    }

    [Test]
    public async Task GetAgentSession_WhenANonMemberSendsAnOutOfRangeLimit_ShouldBeNotFoundNotAValidationError()
    {
        var (key, session) = await ArrangeSessionInPartAsync();

        var byItem = await CallAsync(TestUser.Joel, $$"""{"item":"{{key}}","limit":500}""");
        var bySession = await CallAsync(TestUser.Joel, $$"""{"session":"{{session}}","limit":0}""");

        byItem.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
        bySession.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
    }

    [Test]
    public async Task GetAgentSession_WhenBothSessionAndItem_ShouldBeAValidationErrorNamingTheField()
    {
        var reply = await CallAsync(TestUser.Joel, $$"""{"session":"{{Guid.NewGuid()}}","item":"SHIP-124"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("validation");
        error.GetProperty("field").GetString().ShouldBe("session");
    }

    [Test]
    public async Task GetAgentSession_WhenNeitherSessionNorItem_ShouldBeAValidationErrorNamingTheField()
    {
        var reply = await CallAsync(TestUser.Joel, "{}");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("validation");
        error.GetProperty("field").GetString().ShouldBe("item");
    }

    [Test]
    public async Task GetAgentSession_WhenCallerIsNotAMemberOfTheItemsProject_ShouldBeNotFound()
    {
        var (key, session) = await ArrangeSessionInPartAsync();

        var byItem = await CallAsync(TestUser.Joel, $$"""{"item":"{{key}}"}""");
        var bySession = await CallAsync(TestUser.Joel, $$"""{"session":"{{session}}"}""");

        byItem.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
        bySession.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
    }

    [Test]
    public async Task GetAgentSession_WhenSessionIsUnknownOrInAProjectTheCallerCannotRead_ShouldGiveTheSameNotFoundMessage()
    {
        var (_, hidden) = await ArrangeSessionInPartAsync();

        var unknown = await CallAsync(TestUser.Joel, $$"""{"session":"{{Guid.NewGuid()}}"}""");
        var unreadable = await CallAsync(TestUser.Joel, $$"""{"session":"{{hidden}}"}""");

        var unknownError = unknown.Structured().GetProperty("error");
        var unreadableError = unreadable.Structured().GetProperty("error");
        unknownError.GetProperty("code").GetString().ShouldBe("not_found");
        unreadableError.GetProperty("message").GetString().ShouldBe(unknownError.GetProperty("message").GetString());
    }

    // The answer for the fixture values in TestSupport (DtoFixtures/mcp/get-agent-session.json): the same fields, names and formats.
    [Test]
    public async Task GetAgentSession_WhenCallerIsAdmin_ShouldReturnTheShapePinnedInTestSupport()
    {
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(
            Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "mcp", "get-agent-session.json")))!["admin"]!.AsObject();
        var started = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        var sessionId = Guid.Parse(expected["session"]!["id"]!.GetValue<string>());
        string key;
        string connectionName;
        await using (var connection = await OpenDatabaseAsync())
        {
            var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
            key = await KeyAsync(connection, item);
            var relay = await AgentSessionSeeds.InsertConnectionAsync(connection);
            connectionName = await connection.QuerySingleAsync<string>("SELECT name FROM agent_connection WHERE id = @relay", new { relay });
            var log = await InsertRunLogAsync(connection, item, expected["session"]!["run_log"]!["attachment"]!.GetValue<string>(), expected["session"]!["run_log"]!["size_bytes"]!.GetValue<long>());
            await new AgentSessionSeeds(item, relay).WithId(sessionId).WithState("failed").WithEndReason(AgentEndReasons.DeliveryFailed)
                .WithEndDetail(expected["session"]!["end_detail"]!.GetValue<string>()).WithRunLogAttachment(log).WithCreatedAt(started).InsertAsync(connection);
            await connection.ExecuteAsync(
                "INSERT INTO agent_activity (id, session_id, type, body, created_at) VALUES (@id, @sessionId, 'error', 'Fake activity body.', @at)",
                new { id = Guid.Parse(expected["activities"]![0]!["id"]!.GetValue<string>()), sessionId, at = started.AddSeconds(5) });
            var delivery = await AgentDeliveryAttemptRows.InsertDeliveryAsync(connection, sessionId);
            await AgentDeliveryAttemptRows.InsertAsync(connection, new AgentDeliveryAttemptCreate(delivery, relay, 1, started, null, "No answer within 5 seconds.", 5000, started.AddSeconds(5)));
        }

        var reply = await CallAsync(TestUser.Joel, $$"""{"item":"{{key}}"}""");

        var actual = JsonNode.Parse(reply.Structured().GetRawText())!.AsObject();
        actual.Remove("project");
        actual.Remove("open_url");
        expected["session"]!["item"] = key;
        expected["session"]!["connection"] = connectionName;
        expected["session"]!["ended"] = started.ToString("O");
        expected["session"]!["end_text"] = AgentEndText.For(AgentEndReasons.DeliveryFailed, expected["session"]!["end_detail"]!.GetValue<string>());
        actual["next_before"].ShouldBeNull("the only page has no cursor");
        actual.Remove("next_before");
        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Test]
    public async Task GetAgentSession_WhenRegistered_ShouldMatchItsLineInTheMcpDocs()
    {
        var listed = await PostAsync(await TokenAsync(TestUser.Joel, ReadOnly), """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        var tool = listed.Json().GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == "get_agent_session");
        var arguments = tool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(p => p.Name).ToList();
        arguments.Order().ShouldBe(["before", "item", "limit", "session"]);

        var docs = await File.ReadAllTextAsync(Path.Combine(RepoPaths.Root, "docs", "mcp.md"));
        var row = docs.Split('\n').Single(line => line.StartsWith("| `get_agent_session` |", StringComparison.Ordinal));

        arguments.ShouldAllBe(argument => row.Contains($"`{argument}`", StringComparison.Ordinal), row);
        row.ShouldContain("| read |");
    }

    [Test]
    public void ServerInstructions_ShouldSendAnAgentToGetAgentSessionToFindWhyARunFailed()
        => McpServerServices.Instructions.ShouldContain("To find out why an agent run failed, call get_agent_session.");

    private async Task<McpReply> CallAsync(TestUser user, string arguments)
        => await CallToolAsync(await TokenAsync(user, ReadOnly), "get_agent_session", arguments);

    private async Task<Page> PageAsync(string arguments)
    {
        var reply = await CallAsync(TestUser.Joel, arguments);
        var body = reply.Structured();
        var next = body.TryGetProperty("next_before", out var cursor) && cursor.ValueKind == JsonValueKind.String ? cursor.GetString() : null;
        return new Page([.. body.GetProperty("activities").EnumerateArray().Select(a => a.GetProperty("body").GetString()!)], next);
    }

    private sealed record Page(IReadOnlyList<string> Bodies, string? Next);

    // A session that failed to deliver after two tries, with a run log and a detail naming the relay host.
    private async Task<(string Key, Guid Session)> ArrangeDeliveryFailedAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        var relay = await AgentSessionSeeds.InsertConnectionAsync(connection);
        var log = await InsertRunLogAsync(connection, item, $"agent-run-{Guid.NewGuid():N}.log", 2048);
        var session = await new AgentSessionSeeds(item, relay).WithState("failed").WithEndReason(AgentEndReasons.DeliveryFailed).WithEndDetail(Detail)
            .WithRunLogAttachment(log).InsertAsync(connection);
        await InsertAttemptsAsync(connection, session, relay);
        return (await KeyAsync(connection, item), session);
    }

    private async Task InsertAttemptsAsync(NpgsqlConnection connection, Guid session, Guid relay)
    {
        var delivery = await AgentDeliveryAttemptRows.InsertDeliveryAsync(connection, session);
        await AgentDeliveryAttemptRows.InsertAsync(connection, new AgentDeliveryAttemptCreate(delivery, relay, 1, Now.AddMinutes(-2), null, AttemptError, 5000, Now.AddMinutes(-1)));
        await AgentDeliveryAttemptRows.InsertAsync(connection, new AgentDeliveryAttemptCreate(delivery, relay, 2, Now.AddMinutes(-1), 503, null, 800, null));
    }

    private static async Task<Guid> InsertRunLogAsync(NpgsqlConnection connection, Guid item, string name, long size)
    {
        var id = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            "INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref) VALUES (@id, @item, @name, 'text/plain', @size, 'ref')",
            new { id, item, name, size });
        return id;
    }

    // activity 1 is the oldest, a second apart.
    private async Task<(string Key, Guid Session)> ArrangeSessionWithActivitiesAsync(int count)
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        var session = await new AgentSessionSeeds(item, await AgentSessionSeeds.InsertConnectionAsync(connection)).InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO agent_activity (id, session_id, type, body, created_at) SELECT gen_random_uuid(), @session, 'thought', 'activity ' || n, @start + n * interval '1 second' FROM generate_series(1, @count) n",
            new { session, count, start = Now.AddHours(-1) });
        return (await KeyAsync(connection, item), session);
    }

    private async Task<(string Key, Guid Session)> ArrangeSessionWithBodyAsync(string body)
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        var session = await new AgentSessionSeeds(item, await AgentSessionSeeds.InsertConnectionAsync(connection)).InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO agent_activity (id, session_id, type, body, created_at) VALUES (gen_random_uuid(), @session, 'response', @body, @at)",
            new { session, body, at = Now.AddMinutes(-1) });
        return (await KeyAsync(connection, item), session);
    }

    private async Task<(string Key, Guid Session)> ArrangeSessionInPartAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Part).WithType("task").InsertAsync(connection);
        var session = await new AgentSessionSeeds(item, await AgentSessionSeeds.InsertConnectionAsync(connection)).InsertAsync(connection);
        return (await KeyAsync(connection, item), session);
    }

    private static Task<string> KeyAsync(NpgsqlConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
}
