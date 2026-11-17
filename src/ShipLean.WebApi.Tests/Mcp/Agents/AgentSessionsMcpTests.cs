using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Agents;

// get_work_item's agent_sessions over /mcp (get_agent_session is in GetAgentSession/). Joel is an admin of SHIP and Priya a member; Joel is
// not in PART. Every test arranges its own item, so its sessions are the only ones it reads.
[TestFixture]
public class AgentSessionsMcpTests : McpFixture
{
    private const string Detail = "relay-host.internal:8443 refused the connection";

    protected override bool WithProbeTools => false;

    private DateTimeOffset Now => Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();

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
            await new AgentSessionSeeds(item, relay).WithState("stale").WithEndReason(AgentEndReasons.Stale).WithCreatedAt(Now.AddHours(-3)).InsertAsync(connection);
            await new AgentSessionSeeds(item, relay).WithState("working").WithCreatedAt(Now.AddHours(-1)).InsertAsync(connection);
        }

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        var sessions = reply.Structured().GetProperty("agent_sessions").EnumerateArray().ToList();
        sessions.Select(s => s.GetProperty("state").GetString()).ShouldBe(["working", "stale"]);
        sessions.Any(s => s.TryGetProperty("ended", out _)).ShouldBeFalse();
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
    }

    // Red until SHIP-1640 replaces the AgentEndText.For stub sentence, which ignores the detail; then it is green with no change here.
    [Test]
    public async Task GetWorkItem_WhenDeliveryFailedAndCallerIsAdmin_ShouldNameTheDetailInEndText()
    {
        var key = await ArrangeDeliveryFailedAsync();

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");

        reply.Structured().GetProperty("agent_sessions")[0].GetProperty("end_text").GetString()!.ShouldContain(Detail);
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
        var oldest = Now.AddDays(-count);
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
