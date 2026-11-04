using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// StartWinnerAsync: the caller's prompt is made only by the starter that wins the race, on the winning transaction.
[TestFixture]
public class AgentStartWinnerTests : ApiFixture
{
    private AgentSessionStarter Starter => Host.Services.GetRequiredService<AgentSessionStarter>();

    [Test]
    public async Task StartWinner_WhenWon_ShouldDeliverThePromptTheFactoryReturned()
    {
        await using var database = await OpenDatabaseAsync();
        var item = await (await AgentDispatchArrange.CreateAsync(database, Api)).CreateItemAsync(Api);

        var outcome = await Starter.StartWinnerAsync(item.Id, SeedIds.Users.Joel, null, null, (_, _) => Task.FromResult("The <winner> prompt."), CancellationToken.None);

        outcome.SessionId.ShouldNotBeNull();
        var body = AgentDispatchArrange.BodyOf((await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem());
        body["guidance"]!.AsArray().Single()!["body"]!.GetValue<string>().ShouldBe("The <winner> prompt.");
    }

    [Test]
    public async Task StartWinner_WhenLiveSession_ShouldNotCallTheFactory()
    {
        await using var database = await OpenDatabaseAsync();
        var item = await (await AgentDispatchArrange.CreateAsync(database, Api)).CreateItemAsync(Api);
        await Starter.StartAsync(item.Id, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None);
        var calls = 0;

        var outcome = await Starter.StartWinnerAsync(item.Id, SeedIds.Users.Joel, null, null, (_, _) => Task.FromResult($"call {++calls}"), CancellationToken.None);

        outcome.Block.ShouldBe(AgentStartBlock.LiveSession);
        calls.ShouldBe(0);
    }

    [Test]
    public async Task StartWinner_WhenConcurrent_ShouldCallTheFactoryOnceAndStartOneSession()
    {
        await using var database = await OpenDatabaseAsync();
        var item = await (await AgentDispatchArrange.CreateAsync(database, Api)).CreateItemAsync(Api);
        var calls = 0;
        var barrier = new TaskCompletionSource();

        var racers = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            await barrier.Task;
            return await Starter.StartWinnerAsync(item.Id, SeedIds.Users.Joel, null, null, (_, _) => Task.FromResult($"call {Interlocked.Increment(ref calls)}"), CancellationToken.None);
        })).ToList();
        barrier.SetResult();
        var outcomes = await Task.WhenAll(racers);

        calls.ShouldBe(1);
        outcomes.Count(o => o.SessionId is not null).ShouldBe(1);
        outcomes.Count(o => o.Block == AgentStartBlock.LiveSession).ShouldBe(5);
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task StartWinner_WhenFactoryThrows_ShouldLeaveNoSessionNoDeliveryAndRollBackItsWrites()
    {
        await using var database = await OpenDatabaseAsync();
        var item = await (await AgentDispatchArrange.CreateAsync(database, Api)).CreateItemAsync(Api);

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => Starter.StartWinnerAsync(item.Id, SeedIds.Users.Joel, null, null, async (transaction, token) =>
        {
            await transaction.Connection!.ExecuteAsync(new CommandDefinition("UPDATE work_item SET title = 'Written by the factory' WHERE id = @id", new { id = item.Id }, transaction, cancellationToken: token));
            throw new InvalidOperationException("The factory failed.");
        }, CancellationToken.None));

        failure.Message.ShouldBe("The factory failed.");
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldBeEmpty();
        (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldBeEmpty();
        (await database.ExecuteScalarAsync<string>("SELECT title FROM work_item WHERE id = @id", new { id = item.Id })).ShouldBe("Add the thing");
    }

    [Test]
    public async Task StartWinner_WhenBlocked_ShouldNotCallTheFactory()
    {
        await using var database = await OpenDatabaseAsync();
        var item = await (await AgentDispatchArrange.CreateAsync(database, Api, connection: false)).CreateItemAsync(Api);
        var calls = 0;

        var outcome = await Starter.StartWinnerAsync(item.Id, SeedIds.Users.Joel, null, null, (_, _) => Task.FromResult($"call {++calls}"), CancellationToken.None);

        outcome.Block.ShouldBe(AgentStartBlock.NoConnection);
        calls.ShouldBe(0);
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task StartWinner_WhenOverride_ShouldUseThatConnection()
    {
        await using var database = await OpenDatabaseAsync();
        var item = await (await AgentDispatchArrange.CreateAsync(database, Api, connection: false)).CreateItemAsync(Api);
        var other = (await AgentConnectionsWire.CreateAsync(Api, $"Relay {Guid.NewGuid():N}")).Connection.Id;

        var outcome = await Starter.StartWinnerAsync(item.Id, SeedIds.Users.Joel, null, other, (_, _) => Task.FromResult("Go."), CancellationToken.None);

        outcome.SessionId.ShouldNotBeNull();
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem().ConnectionId.ShouldBe(other);
    }
}
