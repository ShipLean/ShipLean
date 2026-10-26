using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Realtime;
using StackExchange.Redis;

namespace ShipLean.WebApi.Tests.Realtime;

// Two instances of one deployment: two hosts on one database and one Redis (under a channel prefix of their own), as
// behind a load balancer with sticky sessions. A user's socket stays on one host while writes and other users go
// through the other.
[TestFixture]
[CancelAfter(30000)]
public class MultiInstanceRealtimeTests
{
    // Short enough that a dead instance is cleaned up well inside HubReceiver's ten-second wait.
    private static readonly PresenceHeartbeatTiming Fast = new(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2));

    private readonly List<HubReceiver> _receivers = [];
    private ClonedDatabase _database = null!;
    private string _redis = null!;
    private ShipLeanHost _a = null!;
    private ShipLeanHost _b = null!;

    [OneTimeSetUp]
    public async Task StartInstancesAsync()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _redis = await RedisServer.ScenarioConnectionStringAsync();
        _a = StartHost();
        _b = StartHost();
    }

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var receiver in _receivers)
        {
            await receiver.DisposeAsync();
        }
        _receivers.Clear();
    }

    [OneTimeTearDown]
    public async Task StopInstancesAsync()
    {
        await _a.DisposeAsync();
        await _b.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task Leave_WhenTheUserIsConnectedToTheOtherHost_ShouldStopThatProjectsEventsThere()
    {
        var watcher = await JoinedAsync(_a, SeedIds.Users.Joel, SeedIds.Projects.Ship);
        var tom = await JoinedAsync(_b, SeedIds.Users.Tom, SeedIds.Projects.Ship, SeedIds.Projects.Pay);
        await tom.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "leavingBoard");
        UserIds(await watcher.NextPresenceAsync("leavingBoard")).ShouldBe([SeedIds.Users.Tom]);
        await Bus(_a).PublishAsync(new StatesChanged(SeedIds.Projects.Ship));
        (await tom.NextAsync("statesChanged")).GetProperty("projectId").GetGuid().ShouldBe(SeedIds.Projects.Ship);

        using var http = _a.CreateClient();
        using var left = await new ApiClient(http).ActingAs(TestUser.Tom).PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/leave");
        left.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Host B takes Tom off the screen after it takes him out of the group, so this list means the group is done.
        UserIds(await watcher.NextPresenceAsync("leavingBoard")).ShouldBeEmpty();
        await Bus(_a).PublishAsync(new StatesChanged(SeedIds.Projects.Ship));
        await Bus(_a).PublishAsync(new StatesChanged(SeedIds.Projects.Pay));

        (await tom.NextAsync("statesChanged")).GetProperty("projectId").GetGuid().ShouldBe(SeedIds.Projects.Pay);
    }

    [Test]
    public async Task Presence_WhenOneUserIsOnEachHost_ShouldListBothOnBoth()
    {
        var joel = await JoinedAsync(_a, SeedIds.Users.Joel, SeedIds.Projects.Ship);
        await joel.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "sharedBoard");
        var alone = await joel.NextPresenceAsync("sharedBoard");
        UserIds(alone).ShouldBe([SeedIds.Users.Joel]);

        var sam = await JoinedAsync(_b, SeedIds.Users.Sam, SeedIds.Projects.Ship);
        await sam.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "sharedBoard");

        var onB = await sam.NextPresenceAsync("sharedBoard");
        var onA = await joel.NextPresenceAsync("sharedBoard");
        UserIds(onB).ShouldBe([SeedIds.Users.Joel, SeedIds.Users.Sam], ignoreOrder: true);
        UserIds(onA).ShouldBe([SeedIds.Users.Joel, SeedIds.Users.Sam], ignoreOrder: true);
        Sequence(onA).ShouldBe(Sequence(onB));
        Sequence(onA).ShouldBeGreaterThan(Sequence(alone));
    }

    [Test]
    public async Task Presence_WhenAHostStops_ShouldDropItsUsersWithinTheHeartbeatTtl()
    {
        var joel = await JoinedAsync(_a, SeedIds.Users.Joel, SeedIds.Projects.Ship);
        await joel.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "stoppingBoard");
        UserIds(await joel.NextPresenceAsync("stoppingBoard")).ShouldBe([SeedIds.Users.Joel]);
        var c = StartHost();
        try
        {
            var priya = await JoinedAsync(c, SeedIds.Users.Priya, SeedIds.Projects.Ship);
            await priya.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "stoppingBoard");
            UserIds(await joel.NextPresenceAsync("stoppingBoard")).ShouldBe([SeedIds.Users.Joel, SeedIds.Users.Priya], ignoreOrder: true);
        }
        finally
        {
            await c.DisposeAsync();
        }

        UserIds(await joel.NextPresenceAsync("stoppingBoard")).ShouldBe([SeedIds.Users.Joel]);
    }

    [Test]
    public async Task Presence_WhenAnInstanceDiedWithoutLeaving_ShouldDropItsEntriesOnceItsHeartbeatExpires()
    {
        var keys = _a.Services.GetRequiredService<RedisRealtime>();
        await using var redis = await ConnectionMultiplexer.ConnectAsync(await RedisServer.ConnectionStringAsync());
        var database = redis.GetDatabase();
        // What a killed instance leaves behind: listed, its heartbeat not yet expired, Priya still on the screen.
        await database.StringSetAsync(keys.AliveKey("ghost"), "1");
        await database.SetAddAsync(keys.InstancesKey, "ghost");
        await database.HashSetAsync(keys.ScreenKey(SeedIds.Projects.Ship, "ghostBoard"), "ghost-connection", $"{SeedIds.Users.Priya} ghost");
        await database.SetAddAsync(keys.ScreensKey, RedisRealtime.ScreenMember(SeedIds.Projects.Ship, "ghostBoard"));
        var joel = await JoinedAsync(_a, SeedIds.Users.Joel, SeedIds.Projects.Ship);
        await joel.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "ghostBoard");
        UserIds(await joel.NextPresenceAsync("ghostBoard")).ShouldBe([SeedIds.Users.Joel, SeedIds.Users.Priya], ignoreOrder: true);

        await database.KeyDeleteAsync(keys.AliveKey("ghost"));

        UserIds(await joel.NextPresenceAsync("ghostBoard")).ShouldBe([SeedIds.Users.Joel]);
        (await database.SetContainsAsync(keys.InstancesKey, "ghost")).ShouldBeFalse();
    }

    private ShipLeanHost StartHost()
        => ShipLeanHost.Start(_database.ConnectionString, TestUser.Joel, services => services.AddSingleton(Fast), redisConnection: _redis);

    private static IProjectEvents Bus(ShipLeanHost host) => host.Services.GetRequiredService<IProjectEvents>();

    private static List<Guid> UserIds(JsonElement presence)
        => presence.GetProperty("userIds").EnumerateArray().Select(e => e.GetGuid()).ToList();

    private static long Sequence(JsonElement presence) => presence.GetProperty("sequence").GetInt64();

    private async Task<HubReceiver> JoinedAsync(ShipLeanHost host, Guid user, params Guid[] projects)
    {
        var receiver = await HubReceiver.StartAsync(host, user);
        _receivers.Add(receiver);
        foreach (var project in projects)
        {
            await receiver.Connection.InvokeAsync("JoinProject", project);
        }
        return receiver;
    }
}
