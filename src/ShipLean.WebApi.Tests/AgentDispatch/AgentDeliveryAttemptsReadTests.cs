using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// C5: IAgentDeliveryAttempts.ForSessionsAsync reads every attempt of the given sessions' deliveries, keyed by session, oldest
// first, with server labels.
[TestFixture]
public class AgentDeliveryAttemptsReadTests : ApiFixture
{
    [Test]
    public async Task ForSessionsAsync_WhenTwoSessionsHaveAttempts_ShouldGroupBySessionOldestFirst()
    {
        await using var database = await OpenDatabaseAsync();
        var connection = await AgentSessionSeeds.InsertConnectionAsync(database);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(database);
        var first = await new AgentSessionSeeds(item, connection).WithState("failed").InsertAsync(database);
        var second = await new AgentSessionSeeds(item, connection).WithState("completed").InsertAsync(database);
        var other = await new AgentSessionSeeds(item, connection).WithState("completed").InsertAsync(database);
        var created = await AgentDeliveryAttemptRows.InsertDeliveryAsync(database, first);
        var prompted = await AgentDeliveryAttemptRows.InsertDeliveryAsync(database, first, seq: 2, action: "prompted");
        var secondDelivery = await AgentDeliveryAttemptRows.InsertDeliveryAsync(database, second, status: "sent");
        var otherDelivery = await AgentDeliveryAttemptRows.InsertDeliveryAsync(database, other, status: "sent");
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        var t0 = now.AddDays(-3);
        // Inserted newest first, so only the read's order can put them oldest first.
        await AgentDeliveryAttemptRows.InsertAsync(database, new(prompted, connection, 1, t0.AddHours(2), null, "No answer within 5 seconds.", 5000, null));
        await AgentDeliveryAttemptRows.InsertAsync(database, new(created, connection, 2, t0.AddHours(1), 502, "Bad gateway.", 40, null));
        await AgentDeliveryAttemptRows.InsertAsync(database, new(created, connection, 1, t0, null, "No answer within 5 seconds.", 5000, t0.AddHours(1)));
        await AgentDeliveryAttemptRows.InsertAsync(database, new(secondDelivery, connection, 1, t0, 202, null, 120, null));
        await AgentDeliveryAttemptRows.InsertAsync(database, new(otherDelivery, connection, 1, t0, 202, null, 120, null));

        var attempts = await Host.Services.GetRequiredService<IAgentDeliveryAttempts>().ForSessionsAsync([first, second], CancellationToken.None);

        var clock = Host.Services.GetRequiredService<TimeProvider>();
        attempts.Select(group => group.Key).ShouldBe([first, second], ignoreOrder: true);
        attempts[first].ToList().ShouldBe(
        [
            new AgentDeliveryAttemptDto("created", 1, t0, DisplayDate.Relative(t0, clock), null, "No answer within 5 seconds.", 5000, t0.AddHours(1), DisplayDate.Relative(t0.AddHours(1), clock)),
            new AgentDeliveryAttemptDto("created", 2, t0.AddHours(1), DisplayDate.Relative(t0.AddHours(1), clock), 502, "Bad gateway.", 40, null, null),
            new AgentDeliveryAttemptDto("prompted", 1, t0.AddHours(2), DisplayDate.Relative(t0.AddHours(2), clock), null, "No answer within 5 seconds.", 5000, null, null),
        ]);
        attempts[second].Select(a => (a.Action, a.HttpStatus, a.Error)).ShouldBe([("created", (int?)202, (string?)null)]);
    }

    [Test]
    public async Task ForSessionsAsync_WhenNoSessions_ShouldBeEmpty()
    {
        var attempts = await Host.Services.GetRequiredService<IAgentDeliveryAttempts>().ForSessionsAsync([], CancellationToken.None);

        attempts.Count.ShouldBe(0);
    }
}
