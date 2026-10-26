using Microsoft.AspNetCore.SignalR.Client;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Tests.Realtime;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The realtime hub takes the ordinary cookie a proxy sign-in issued, with no identity on the connection itself.
[TestFixture]
[CancelAfter(30000)]
public class ProxySignInHubTests : ProxyApiFixture
{
    [Test]
    public async Task Hub_WhenCookieFromProxySignIn_ShouldConnect()
    {
        using var client = ProxyClient().WithIdentity(TestUser.Joel.Email!);
        await client.GetJsonAsync<Me>("/api/me");
        var issued = client.Cookie(AuthServices.CookieName)!;

        await using var receiver = await HubReceiver.StartAsync(Host, configure: options => options.Headers["Cookie"] = $"{issued.Name}={issued.Value}");
        await receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);

        receiver.Connection.State.ShouldBe(HubConnectionState.Connected);
    }

    // The /api/me fields these tests read (MeDto's enums are strings on the wire).
    private sealed record Me(Guid Id, string Email, string Name, bool HasProjects);
}
