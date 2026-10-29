using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// AgentSessionStarter called with a connection and a prompt of its own, as an automation will.
[TestFixture]
public class AgentStartOverrideTests : ApiFixture
{
    private AgentSessionStarter Starter => Host.Services.GetRequiredService<AgentSessionStarter>();

    private async Task<Guid> NewConnectionAsync() => (await AgentConnectionsWire.CreateAsync(Api, $"Relay {Guid.NewGuid():N}")).Connection.Id;

    [Test]
    public async Task Plan_WhenAnOverrideConnectionIsGiven_ShouldPlanWithItInsteadOfTheProjects()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var other = await NewConnectionAsync();

        var plan = await Starter.PlanAsync(item.Id, other, null, CancellationToken.None);

        plan.Block.ShouldBeNull();
        plan.ConnectionId.ShouldBe(other);
    }

    [Test]
    public async Task Plan_WhenTheOverrideConnectionIsArchived_ShouldBeNoConnectionEvenIfTheProjectsIsLive()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var other = await NewConnectionAsync();
        await database.ExecuteAsync("UPDATE agent_connection SET archived_at = now() WHERE id = @other", new { other });

        var plan = await Starter.PlanAsync(item.Id, other, null, CancellationToken.None);

        plan.Block.ShouldBe(AgentStartBlock.NoConnection);
    }

    [Test]
    public async Task Plan_WhenTheProjectHasNoConnectionButOneIsGiven_ShouldPlanWithTheGivenOne()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api, connection: false);
        var item = await arranged.CreateItemAsync(Api);
        var other = await NewConnectionAsync();

        var plan = await Starter.PlanAsync(item.Id, other, null, CancellationToken.None);

        plan.Block.ShouldBeNull();
    }

    [Test]
    public async Task Start_WhenAnOverrideConnectionAndPromptAreGiven_ShouldStoreTheConnectionAndSendThePromptAsGuidance()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var other = await NewConnectionAsync();

        var outcome = await Starter.StartAsync(item.Id, ShipLean.TestSupport.SeedIds.Users.Joel, null, other, "Do the <thing>.", null, CancellationToken.None);

        outcome.SessionId.ShouldNotBeNull();
        var session = (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        var stored = await database.ExecuteScalarAsync<Guid>("SELECT connection_id FROM agent_session WHERE id = @id", new { id = session.Id });
        stored.ShouldBe(other);
        var body = AgentDispatchArrange.BodyOf((await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem());
        body["guidance"]!.AsArray().Single()!["body"]!.GetValue<string>().ShouldBe("Do the <thing>.");
        body["appUserId"]!.GetValue<string>().ShouldBe(other.ToString());
        body["promptContext"]!.GetValue<string>().ShouldContain("<guidance>Do the &lt;thing&gt;.</guidance>");
    }

    [Test]
    public async Task Start_WhenNoOverrideIsGiven_ShouldUseTheProjectsConnectionAndEmptyGuidance()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);

        await Starter.StartAsync(item.Id, ShipLean.TestSupport.SeedIds.Users.Joel, null, null, "  ", null, CancellationToken.None);

        var body = AgentDispatchArrange.BodyOf((await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem());
        body["appUserId"]!.GetValue<string>().ShouldBe(arranged.ConnectionId.ToString());
        body["guidance"]!.AsArray().ShouldBeEmpty();
    }
}
