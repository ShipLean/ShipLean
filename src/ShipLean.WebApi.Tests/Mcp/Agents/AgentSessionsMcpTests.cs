using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Agents;

// get_work_item's agent_sessions and the get_agent_session shell over /mcp. Joel is an admin of SHIP and Priya a member; Joel is
// not in PART. Every test arranges its own item, so its sessions are the only ones it reads.
[TestFixture]
public class AgentSessionsMcpTests : McpFixture
{
    private const string Detail = "relay-host.internal:8443 refused the connection";

    protected override bool WithProbeTools => false;

    [Test]
    public async Task GetWorkItem_WhenSevenSessions_ShouldListTheNewestFiveNewestFirst()
    {
        var (key, ids) = await ArrangeSessionsAsync(7);

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        var sessions = reply.Structured().GetProperty("agent_sessions").EnumerateArray().ToList();
        sessions.Select(s => s.GetProperty("id").GetGuid()).ShouldBe(ids.Take(5), "ids are newest first");
        sessions[0].GetProperty("connection").GetString().ShouldNotBeNullOrEmpty();
        sessions[0].GetProperty("state").GetString().ShouldBe("completed");
        sessions[0].GetProperty("end_reason").GetString().ShouldBe(AgentEndReasons.Completed);
        sessions[0].GetProperty("end_text").GetString().ShouldNotBeNullOrEmpty();
        sessions[0].GetProperty("ended").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task GetWorkItem_WhenNoSessions_ShouldListNone()
    {
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            key = await KeyAsync(connection, await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection));
        }

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        reply.Structured().GetProperty("agent_sessions").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task GetWorkItem_WhenSessionIsStaleOrLive_ShouldHaveNoEnded()
    {
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
            key = await KeyAsync(connection, item);
            var relay = await AgentSessionSeeds.InsertConnectionAsync(connection);
            await new AgentSessionSeeds(item, relay).WithState("stale").WithEndReason(AgentEndReasons.Stale).WithCreatedAt(DateTimeOffset.UtcNow.AddHours(-3)).InsertAsync(connection);
            await new AgentSessionSeeds(item, relay).WithState("working").WithCreatedAt(DateTimeOffset.UtcNow.AddHours(-1)).InsertAsync(connection);
        }

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        var sessions = reply.Structured().GetProperty("agent_sessions").EnumerateArray().ToList();
        sessions.Select(s => s.GetProperty("state").GetString()).ShouldBe(["working", "stale"]);
        sessions.ShouldAllBe(s => !s.TryGetProperty("ended", out _));
        sessions[0].TryGetProperty("end_reason", out _).ShouldBeFalse();
        sessions[1].GetProperty("end_reason").GetString().ShouldBe(AgentEndReasons.Stale);
    }

    [Test]
    public async Task GetWorkItem_WhenDeliveryFailedAndCallerIsMember_ShouldKeepTheDetailOutOfEndText()
    {
        var key = await ArrangeDeliveryFailedAsync();

        var reply = await CallToolAsync(await TokenAsync(TestUser.Priya, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        var session = reply.Structured().GetProperty("agent_sessions")[0];
        session.GetProperty("end_reason").GetString().ShouldBe(AgentEndReasons.DeliveryFailed);
        session.GetProperty("end_text").GetString().ShouldBe(AgentEndText.For(AgentEndReasons.DeliveryFailed, null));
        reply.Texts().ShouldAllBe(text => !text.Contains(Detail));
    }

    [Test]
    public async Task GetWorkItem_WhenDeliveryFailedAndCallerIsAdmin_ShouldUseTheDetailInEndText()
    {
        var key = await ArrangeDeliveryFailedAsync();

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        var text = reply.Structured().GetProperty("agent_sessions")[0].GetProperty("end_text").GetString();
        text.ShouldBe(AgentEndText.For(AgentEndReasons.DeliveryFailed, Detail));
        // TODO: red until the per-reason wording makes AgentEndText.For carry the detail (its stub sentence ignores it).
        text.ShouldContain(Detail);
    }

    [Test]
    public async Task GetAgentSession_WhenBothSessionAndItem_ShouldBeAValidationErrorNamingTheField()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_agent_session", $$"""{"session":"{{Guid.NewGuid()}}","item":"SHIP-124"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("validation");
        error.GetProperty("field").GetString().ShouldBe("session");
    }

    [Test]
    public async Task GetAgentSession_WhenNeitherSessionNorItem_ShouldBeAValidationErrorNamingTheField()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_agent_session", "{}");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("validation");
        error.GetProperty("field").GetString().ShouldBe("item");
    }

    [TestCase(0)]
    [TestCase(101)]
    public async Task GetAgentSession_WhenLimitOutOfRange_ShouldBeAValidationErrorNamingLimit(int limit)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_agent_session", $$"""{"item":"SHIP-124","limit":{{limit}}}""");

        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("limit");
    }

    [Test]
    public async Task GetAgentSession_WhenCallerIsNotAMemberOfTheItemsProject_ShouldBeNotFound()
    {
        Guid session;
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            var item = await new WorkItemBuilder(SeedIds.Projects.Part).WithType("task").InsertAsync(connection);
            key = await KeyAsync(connection, item);
            session = await new AgentSessionSeeds(item, await AgentSessionSeeds.InsertConnectionAsync(connection)).InsertAsync(connection);
        }
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var byItem = await CallToolAsync(token, "get_agent_session", $$"""{"item":"{{key}}"}""");
        var bySession = await CallToolAsync(token, "get_agent_session", $$"""{"session":"{{session}}"}""");

        byItem.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
        bySession.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
    }

    [Test]
    public async Task GetAgentSession_WhenSessionIdIsUnknown_ShouldBeNotFound()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_agent_session", $$"""{"session":"{{Guid.NewGuid()}}"}""");

        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
    }

    [Test]
    public async Task GetAgentSession_WhenCallerIsMemberAndEndIsDeliveryFailed_ShouldHideDetailRunLogAndDeliveries()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Priya, ReadOnly), "get_agent_session", """{"item":"SHIP-124"}""");

        var session = reply.Structured().GetProperty("session");
        session.GetProperty("end_reason").GetString().ShouldBe(AgentEndReasons.DeliveryFailed);
        session.GetProperty("end_text").GetString().ShouldBe(AgentEndText.For(AgentEndReasons.DeliveryFailed, null));
        session.TryGetProperty("end_detail", out _).ShouldBeFalse();
        session.TryGetProperty("run_log", out _).ShouldBeFalse();
        reply.Structured().GetProperty("deliveries").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task GetAgentSession_WhenCallerIsAdmin_ShouldReturnTheShapePinnedInTestSupport()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_agent_session", """{"item":"SHIP-124"}""");

        var actual = System.Text.Json.Nodes.JsonNode.Parse(reply.Structured().GetRawText())!.AsObject();
        actual.Remove("project");
        actual.Remove("open_url");
        var expected = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(
            Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "mcp", "get-agent-session.json")))!["admin"]!.AsObject();
        expected["session"]!["end_text"] = AgentEndText.For(AgentEndReasons.DeliveryFailed, expected["session"]!["end_detail"]!.GetValue<string>());
        System.Text.Json.Nodes.JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    private async Task<string> ArrangeDeliveryFailedAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        await new AgentSessionSeeds(item, await AgentSessionSeeds.InsertConnectionAsync(connection))
            .WithState("failed").WithEndReason(AgentEndReasons.DeliveryFailed).WithEndDetail(Detail).InsertAsync(connection);
        return await KeyAsync(connection, item);
    }

    // Item key and the session ids, newest first. Sessions are completed, a day apart.
    private async Task<(string Key, IReadOnlyList<Guid> Ids)> ArrangeSessionsAsync(int count)
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        var relay = await AgentSessionSeeds.InsertConnectionAsync(connection);
        var oldest = DateTimeOffset.UtcNow.AddDays(-count);
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            ids.Add(await new AgentSessionSeeds(item, relay).WithEndReason(AgentEndReasons.Completed).WithCreatedAt(oldest.AddDays(i)).InsertAsync(connection));
        }

        ids.Reverse();
        return (await KeyAsync(connection, item), ids);
    }

    private static Task<string> KeyAsync(NpgsqlConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
}
