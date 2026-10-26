using Dapper;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

// The backstop for a member removal that never reached an instance (a lost Redis message): the sweep re-checks the
// membership of every project a connection joined. No Redis: the row is archived behind the hub's back instead.
[TestFixture]
[CancelAfter(30000)]
public class HubMembershipSweepTests : ApiFixture
{
    [Test]
    public async Task Sweep_WhenAJoinedUserIsNoLongerAMember_ShouldStopThatProjectsEvents()
    {
        await using var db = await OpenDatabaseAsync();
        var userId = await new AppUserBuilder().InsertAsync(db);
        foreach (var projectId in new[] { SeedIds.Projects.Ship, SeedIds.Projects.Pay })
        {
            await db.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @userId, 'member')",
                new { id = Guid.NewGuid(), projectId, userId });
        }
        await using var member = await HubReceiver.StartAsync(Host, userId);
        await member.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);
        await member.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Pay);
        var bus = Host.Services.GetRequiredService<IProjectEvents>();
        await bus.PublishAsync(new StatesChanged(SeedIds.Projects.Ship));
        (await member.NextAsync("statesChanged")).GetProperty("projectId").GetGuid().ShouldBe(SeedIds.Projects.Ship);
        await db.ExecuteAsync(
            "UPDATE project_member SET archived_at = now() WHERE project_id = @projectId AND user_id = @userId",
            new { projectId = SeedIds.Projects.Ship, userId });

        await Host.Services.GetRequiredService<HubSessionSweep>().SweepAsync(CancellationToken.None);
        await bus.PublishAsync(new StatesChanged(SeedIds.Projects.Ship));
        await bus.PublishAsync(new StatesChanged(SeedIds.Projects.Pay));

        (await member.NextAsync("statesChanged")).GetProperty("projectId").GetGuid().ShouldBe(SeedIds.Projects.Pay);
    }
}
