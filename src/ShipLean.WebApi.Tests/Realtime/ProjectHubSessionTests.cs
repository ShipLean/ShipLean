using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

// A hub connection outlives the cookie check made when it opened, so a session that ends afterwards must stop working
// within the freshness controllers have, and the hub is WebSockets only so the user is always the connection's own.
[TestFixture]
[CancelAfter(30000)]
public class ProjectHubSessionTests : ApiFixture
{
    [Test]
    public async Task Hub_WhenStampRotatedAfterConnect_ShouldRefuseNextCall()
    {
        var (userId, receiver) = await ConnectedMemberAsync();
        await receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);
        var closed = ClosedAsync(receiver);
        await using (var db = await OpenDatabaseAsync())
        {
            await db.ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });
        }
        Forget(userId);

        // The call is refused and the socket dropped. The drop can overtake the refusal's reply, in which case the client
        // sees the call cancelled; its reconnect then gets 401 session_ended from negotiate.
        var refused = await Should.ThrowAsync<Exception>(() => receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship));

        (refused is HubException ? refused.Message : "HubException: session_ended: ").ShouldContain("HubException: session_ended: ");
        await closed;
        receiver.Connection.State.ShouldBe(HubConnectionState.Disconnected);
    }

    [Test]
    public async Task Hub_WhenUserDisabledAfterConnect_ShouldDropWithinSweep()
    {
        var (userId, receiver) = await ConnectedMemberAsync();
        await receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);
        var closed = ClosedAsync(receiver);
        await Host.Services.GetRequiredService<HubSessionSweep>().SweepAsync(CancellationToken.None);
        receiver.Connection.State.ShouldBe(HubConnectionState.Connected);
        await using (var db = await OpenDatabaseAsync())
        {
            await db.ExecuteAsync("UPDATE app_user SET disabled_at = now() WHERE id = @userId", new { userId });
        }
        Forget(userId);

        await Host.Services.GetRequiredService<HubSessionSweep>().SweepAsync(CancellationToken.None);

        await closed;
        receiver.Connection.State.ShouldBe(HubConnectionState.Disconnected);
    }

    [Test]
    public async Task Hub_WhenOneTimePasswordSetAfterConnect_ShouldRefuseNextCall()
    {
        var (userId, receiver) = await ConnectedMemberAsync();
        await receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);
        await using (var db = await OpenDatabaseAsync())
        {
            await db.ExecuteAsync("UPDATE app_user SET must_change_password = true WHERE id = @userId", new { userId });
        }
        Forget(userId);

        var refused = await Should.ThrowAsync<HubException>(() => receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship));

        refused.Message.ShouldContain("HubException: password_change_required: ");
    }

    [Test]
    public async Task Hub_WhenLongPolling_ShouldBeRefused()
    {
        using var http = new HttpClient { BaseAddress = new Uri(Host.BaseUrl) };
        using var negotiate = await http.PostAsync($"{ProjectHub.Path}/negotiate?negotiateVersion=1", null);
        var body = await negotiate.Content.ReadFromJsonAsync<JsonElement>();
        var transports = body.GetProperty("availableTransports").EnumerateArray().Select(t => t.GetProperty("transport").GetString()).ToList();

        transports.ShouldBe(["WebSockets"]);
        await Should.ThrowAsync<Exception>(() => HubReceiver.StartAsync(Host, configure: options => options.Transports = HttpTransportType.LongPolling));
    }

    [Test]
    public async Task JoinProject_WhenNonMemberOverWebSocket_ShouldFail()
    {
        // The host's default user is Joel, who is a member of Ship; the connection is Kai, who is not.
        await using var kai = await HubReceiver.StartAsync(Host, SeedIds.Users.Kai);

        var refused = await Should.ThrowAsync<HubException>(() => kai.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship));

        refused.Message.ShouldEndWith("HubException: not_found: That project or item was not found.");
    }

    // A fresh user who is a member of Ship, connected with a real cookie (so the principal carries a security stamp).
    private async Task<(Guid UserId, HubReceiver Receiver)> ConnectedMemberAsync()
    {
        await using var db = await OpenDatabaseAsync();
        var builder = new AppUserBuilder();
        var userId = await builder.InsertAsync(db);
        await db.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @userId, 'member')",
            new { id = Guid.NewGuid(), projectId = SeedIds.Projects.Ship, userId });
        var cookie = await HubSignIn.CookieAsync(Host, builder.Email, AppUserBuilder.ArrangedPassword);
        var receiver = await HubReceiver.StartAsync(Host, configure: options => options.Headers["Cookie"] = cookie);
        return (userId, receiver);
    }

    private static Task ClosedAsync(HubReceiver receiver)
    {
        var closed = new TaskCompletionSource();
        new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token.Register(() => closed.TrySetCanceled());
        receiver.Connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        return closed.Task;
    }

    private void Forget(Guid userId) => Host.Services.GetRequiredService<UserSessions>().Forget(userId);
}
