using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

// /hubs/project over a real WebSocket against the real app, signed in through the test scheme (Joel unless a receiver
// names another user).
[TestFixture]
[CancelAfter(30000)]
public class ProjectHubTests : ApiFixture
{
    private readonly List<HubReceiver> _receivers = [];

    private IProjectEvents Bus => Host.Services.GetRequiredService<IProjectEvents>();

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var receiver in _receivers)
        {
            await receiver.DisposeAsync();
        }
        _receivers.Clear();
    }

    [Test]
    public async Task JoinProject_WhenNotMember_ShouldFailWithNotFound()
    {
        var kai = await ConnectAsync(SeedIds.Users.Kai);
        var unknown = await ConnectAsync();

        var refused = await Should.ThrowAsync<HubException>(() => kai.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship));
        var missing = await Should.ThrowAsync<HubException>(() => unknown.Connection.InvokeAsync("JoinProject", Guid.NewGuid()));

        // SignalR wraps the hub's message: clients parse it with /HubException: ([a-z_]+): / (docs/events.md).
        refused.Message.ShouldEndWith("HubException: not_found: That project or item was not found.");
        missing.Message.ShouldBe(refused.Message);
    }

    [Test]
    public async Task Publish_WhenEventOnBus_ShouldReachJoinedMembersOnly()
    {
        var onShip = await JoinedAsync(SeedIds.Projects.Ship);
        var onPay = await JoinedAsync(SeedIds.Projects.Pay);
        var notJoined = await ConnectAsync();
        var sprints = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        await Bus.PublishAsync(new SprintChanged(SeedIds.Projects.Ship, sprints[0]));
        await Bus.PublishAsync(new SprintChanged(SeedIds.Projects.Pay, sprints[1]));
        await Bus.PublishAsync(new SprintChanged(SeedIds.Projects.Ship, sprints[2]));

        (await onShip.NextAsync("sprintChanged")).GetProperty("sprintId").GetGuid().ShouldBe(sprints[0]);
        (await onShip.NextAsync("sprintChanged")).GetProperty("sprintId").GetGuid().ShouldBe(sprints[2]);
        (await onPay.NextAsync("sprintChanged")).GetProperty("sprintId").GetGuid().ShouldBe(sprints[1]);
        await notJoined.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Pay);
        await Bus.PublishAsync(new SprintChanged(SeedIds.Projects.Pay, sprints[0]));
        (await notJoined.NextAsync("sprintChanged")).GetProperty("sprintId").GetGuid().ShouldBe(sprints[0]);
    }

    [Test]
    public async Task LeaveProject_WhenCalled_ShouldStopThatProjectsEvents()
    {
        var leaver = await JoinedAsync(SeedIds.Projects.Ship, SeedIds.Projects.Pay);
        await leaver.Connection.InvokeAsync("LeaveProject", SeedIds.Projects.Ship);

        await Bus.PublishAsync(new StatesChanged(SeedIds.Projects.Ship));
        await Bus.PublishAsync(new StatesChanged(SeedIds.Projects.Pay));

        (await leaver.NextAsync("statesChanged")).GetProperty("projectId").GetGuid().ShouldBe(SeedIds.Projects.Pay);
    }

    [Test]
    public async Task RemoveUser_WhenMemberWasJoined_ShouldStopThatProjectsEvents()
    {
        var member = await JoinedAsync(SeedIds.Projects.Ship, SeedIds.Projects.Pay);

        await Host.Services.GetRequiredService<ProjectGroupMembers>().RemoveUserAsync(SeedIds.Projects.Ship, SeedIds.Users.Joel);
        await Bus.PublishAsync(new StatesChanged(SeedIds.Projects.Ship));
        await Bus.PublishAsync(new StatesChanged(SeedIds.Projects.Pay));

        (await member.NextAsync("statesChanged")).GetProperty("projectId").GetGuid().ShouldBe(SeedIds.Projects.Pay);
    }

    [Test]
    public async Task Relay_ShouldSendTheTypeDiscriminatorAndCamelCaseEnums()
    {
        var receiver = await JoinedAsync(SeedIds.Projects.Ship);
        var id = Guid.NewGuid();

        await Bus.PublishAsync(new WorkItemCreated(SeedIds.Projects.Ship, id, "SHIP-1", WorkItemTypeId.Story, null) { ClientId = "tab-1" });

        var received = await receiver.NextAsync("workItemCreated");
        received.GetProperty("type").GetString().ShouldBe("workItemCreated");
        received.GetProperty("workItemType").GetString().ShouldBe("story");
        received.GetProperty("id").GetGuid().ShouldBe(id);
        received.GetProperty("clientId").GetString().ShouldBe("tab-1");
        received.GetProperty("parentId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Presence_WhenScreenJoined_ShouldListEveryoneOnIt()
    {
        var watcher = await JoinedAsync(SeedIds.Projects.Ship);
        var joel = await ConnectAsync();
        var mei = await ConnectAsync(SeedIds.Users.Mei);

        await joel.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "listingBacklog");
        await mei.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "listingBacklog");
        await mei.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "listingBoard");

        UserIds(await watcher.NextPresenceAsync("listingBacklog")).ShouldBe([SeedIds.Users.Joel]);
        UserIds(await watcher.NextPresenceAsync("listingBacklog")).ShouldBe([SeedIds.Users.Joel, SeedIds.Users.Mei], ignoreOrder: true);
        UserIds(await watcher.NextPresenceAsync("listingBoard")).ShouldBe([SeedIds.Users.Mei]);
    }

    [Test]
    public async Task Presence_WhenSameUserHasTwoTabsOnAScreen_ShouldListThemOnce()
    {
        var watcher = await JoinedAsync(SeedIds.Projects.Ship);
        var tabs = new[] { await ConnectAsync(), await ConnectAsync() };

        foreach (var tab in tabs)
        {
            await tab.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "twoTabs");
        }
        await tabs[0].Connection.InvokeAsync("LeaveScreen", SeedIds.Projects.Ship, "twoTabs");

        await watcher.NextPresenceAsync("twoTabs");
        UserIds(await watcher.NextPresenceAsync("twoTabs")).ShouldBe([SeedIds.Users.Joel]);
        UserIds(await watcher.NextPresenceAsync("twoTabs")).ShouldBe([SeedIds.Users.Joel]);
    }

    [Test]
    public async Task Presence_WhenDisconnected_ShouldDropTheUser()
    {
        var watcher = await JoinedAsync(SeedIds.Projects.Ship);
        var mei = await ConnectAsync(SeedIds.Users.Mei);
        await mei.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "dropping");
        UserIds(await watcher.NextPresenceAsync("dropping")).ShouldBe([SeedIds.Users.Mei]);

        await mei.Connection.StopAsync();

        UserIds(await watcher.NextPresenceAsync("dropping")).ShouldBeEmpty();
    }

    [TestCase("")]
    [TestCase("has space")]
    [TestCase("with-dash")]
    [TestCase("digits1")]
    [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task JoinScreen_WhenIdInvalid_ShouldFail(string screenId)
    {
        var joel = await ConnectAsync();

        var refused = await Should.ThrowAsync<HubException>(() => joel.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, screenId));

        refused.Message.ShouldContain("HubException: validation: ");
    }

    [Test]
    public async Task JoinScreen_WhenSecondScreen_ShouldLeaveFirst()
    {
        var watcher = await JoinedAsync(SeedIds.Projects.Ship);
        var mei = await ConnectAsync(SeedIds.Users.Mei);

        await mei.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "firstScreen");
        await mei.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "secondScreen");

        UserIds(await watcher.NextPresenceAsync("firstScreen")).ShouldBe([SeedIds.Users.Mei]);
        UserIds(await watcher.NextPresenceAsync("firstScreen")).ShouldBeEmpty();
        UserIds(await watcher.NextPresenceAsync("secondScreen")).ShouldBe([SeedIds.Users.Mei]);
    }

    [Test]
    public async Task LeaveProject_WhenOnAScreen_ShouldLeaveTheScreen()
    {
        var watcher = await JoinedAsync(SeedIds.Projects.Ship);
        var mei = await ConnectAsync(SeedIds.Users.Mei);
        await mei.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);
        await mei.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "leavingProject");
        UserIds(await watcher.NextPresenceAsync("leavingProject")).ShouldBe([SeedIds.Users.Mei]);

        await mei.Connection.InvokeAsync("LeaveProject", SeedIds.Projects.Ship);

        UserIds(await watcher.NextPresenceAsync("leavingProject")).ShouldBeEmpty();
    }

    [Test]
    public async Task RemoveUser_WhenOnAScreen_ShouldLeaveTheScreen()
    {
        var watcher = await JoinedAsync(SeedIds.Projects.Ship);
        var mei = await ConnectAsync(SeedIds.Users.Mei);
        await mei.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "removedUser");
        UserIds(await watcher.NextPresenceAsync("removedUser")).ShouldBe([SeedIds.Users.Mei]);

        await Host.Services.GetRequiredService<ProjectGroupMembers>().RemoveUserAsync(SeedIds.Projects.Ship, SeedIds.Users.Mei);

        UserIds(await watcher.NextPresenceAsync("removedUser")).ShouldBeEmpty();
    }

    [Test]
    public async Task JoinScreen_WhenNotMember_ShouldFailWithNotFound()
    {
        var kai = await ConnectAsync(SeedIds.Users.Kai);

        var refused = await Should.ThrowAsync<HubException>(() => kai.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "backlog"));

        refused.Message.ShouldEndWith("HubException: not_found: That project or item was not found.");
    }

    [Test]
    public async Task JoinProject_WhenRemovedMemberRejoins_ShouldFail()
    {
        await using var db = await OpenDatabaseAsync();
        var userId = await new AppUserBuilder().InsertAsync(db);
        await db.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @userId, 'member')",
            new { id = Guid.NewGuid(), projectId = SeedIds.Projects.Ship, userId });
        var member = await ConnectAsync(userId);
        await member.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);
        await member.Connection.InvokeAsync("LeaveProject", SeedIds.Projects.Ship);
        await db.ExecuteAsync("UPDATE project_member SET archived_at = now() WHERE user_id = @userId", new { userId });

        var refused = await Should.ThrowAsync<HubException>(() => member.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship));

        refused.Message.ShouldContain("not_found: ");
    }

    private static List<Guid> UserIds(JsonElement presence)
        => presence.GetProperty("userIds").EnumerateArray().Select(e => e.GetGuid()).ToList();

    private async Task<HubReceiver> ConnectAsync(Guid? user = null)
    {
        var receiver = await HubReceiver.StartAsync(Host, user);
        _receivers.Add(receiver);
        return receiver;
    }

    private async Task<HubReceiver> JoinedAsync(params Guid[] projects)
    {
        var receiver = await ConnectAsync();
        foreach (var project in projects)
        {
            await receiver.Connection.InvokeAsync("JoinProject", project);
        }
        return receiver;
    }
}
