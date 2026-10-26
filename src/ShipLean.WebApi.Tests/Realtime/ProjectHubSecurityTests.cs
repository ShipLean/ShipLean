using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Connections;
using Dapper;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Realtime;

// Who may open /hubs/project at all: a signed-in user whose session is live, who is not on a one-time password, from
// the site's own origin (or no origin). The negotiate request is the one that is refused, so the client sees the status.
[TestFixture]
[CancelAfter(30000)]
public class ProjectHubSecurityTests : ApiFixture
{
    [Test]
    public async Task Hub_WhenNoUser_ShouldRefuse()
    {
        var failure = await Should.ThrowAsync<HttpRequestException>(() => HubReceiver.StartAsync(Host, configure: options => options.Headers[TestAuthentication.UserHeader] = TestAuthentication.AnonymousValue));

        failure.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Hub_WhenPasswordChangeRequired_ShouldRefuse()
    {
        var cookie = await HubSignIn.CookieAsync(Host, "ravi@shiplean.local", SeedIds.Passwords.Ravi);

        var failure = await Should.ThrowAsync<HttpRequestException>(() => HubReceiver.StartAsync(Host, configure: options => options.Headers["Cookie"] = cookie));

        failure.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Hub_WhenSessionEnded_ShouldRefuse()
    {
        await using var db = await OpenDatabaseAsync();
        var builder = new AppUserBuilder();
        var userId = await builder.InsertAsync(db);
        var cookie = await HubSignIn.CookieAsync(Host, builder.Email, AppUserBuilder.ArrangedPassword);
        await using (var connected = await HubReceiver.StartAsync(Host, configure: options => options.Headers["Cookie"] = cookie))
        {
            connected.Connection.State.ShouldBe(HubConnectionState.Connected);
        }
        await db.ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });
        Host.Services.GetRequiredService<UserSessions>().Forget(userId);

        var failure = await Should.ThrowAsync<HttpRequestException>(() => HubReceiver.StartAsync(Host, configure: options => options.Headers["Cookie"] = cookie));

        failure.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase("https://evil.example")]
    [TestCase("null")]
    [TestCase("http://127.0.0.1:1")]
    [TestCase("https://127.0.0.1")]
    public async Task Hub_WhenForeignOrigin_ShouldRefuse(string origin)
    {
        var failure = await Should.ThrowAsync<HttpRequestException>(() => HubReceiver.StartAsync(Host, configure: options => options.Headers["Origin"] = origin));

        failure.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Hub_WhenNoOrigin_ShouldConnect()
    {
        await using var receiver = await HubReceiver.StartAsync(Host);

        receiver.Connection.State.ShouldBe(HubConnectionState.Connected);
    }

    [Test]
    public async Task Hub_WhenOwnOrigin_ShouldConnect()
    {
        await using var receiver = await HubReceiver.StartAsync(Host, configure: options => options.Headers["Origin"] = Host.BaseUrl);

        receiver.Connection.State.ShouldBe(HubConnectionState.Connected);
    }

    // The same three refusals on a WebSocket opened without negotiating, as a script would.
    [Test]
    public async Task Hub_WhenAnonymousOverDirectWebSocket_ShouldRefuse()
    {
        var failure = await Should.ThrowAsync<WebSocketException>(() => HubReceiver.StartAsync(Host, configure: options =>
        {
            Direct(options);
            options.Headers[TestAuthentication.UserHeader] = TestAuthentication.AnonymousValue;
        }));

        failure.Message.ShouldContain("401");
    }

    [Test]
    public async Task Hub_WhenPasswordChangeRequiredOverDirectWebSocket_ShouldRefuse()
    {
        var cookie = await HubSignIn.CookieAsync(Host, "ravi@shiplean.local", SeedIds.Passwords.Ravi);

        var failure = await Should.ThrowAsync<WebSocketException>(() => HubReceiver.StartAsync(Host, configure: options =>
        {
            Direct(options);
            options.Headers["Cookie"] = cookie;
        }));

        failure.Message.ShouldContain("403");
    }

    [Test]
    public async Task Hub_WhenForeignOriginOverDirectWebSocket_ShouldRefuse()
    {
        var failure = await Should.ThrowAsync<WebSocketException>(() => HubReceiver.StartAsync(Host, configure: options =>
        {
            Direct(options);
            options.Headers["Origin"] = "https://evil.example";
        }));

        failure.Message.ShouldContain("403");
    }

    private static void Direct(Microsoft.AspNetCore.Http.Connections.Client.HttpConnectionOptions options)
    {
        options.SkipNegotiation = true;
        options.Transports = HttpTransportType.WebSockets;
    }
}
