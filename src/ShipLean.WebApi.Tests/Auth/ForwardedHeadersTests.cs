using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.Http.Json;
using ShipLean.Contracts;
using ShipLean.Contracts.Auth;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// Which forwarded headers the app believes. The test peer is loopback, which is always trusted (a proxy on the same
// box); the same app reached through the machine's other address is an untrusted peer.
[TestFixture]
public class ForwardedHeadersTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override bool ListenOnAllInterfaces => true;

    [Test]
    public async Task Login_WhenOneClientFailsThirtyTimes_ShouldNotLimitAnother()
    {
        using var client = AuthClient.Create(Host);
        for (var i = 0; i < 30; i++)
        {
            (await LoginAsync(client, $"first-{i}@example.test", "203.0.113.1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var sameClient = await LoginAsync(client, "first-30@example.test", "203.0.113.1");
        using var otherClient = await LoginAsync(client, "second-0@example.test", "203.0.113.2");

        sameClient.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        otherClient.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_WhenUntrustedPeerSpoofsForwardedFor_ShouldIgnoreIt()
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri($"http://{OtherLocalAddress()}:{new Uri(Host.BaseUrl).Port}") };
        client.DefaultRequestHeaders.Add(ClientHeader.Name, "auth-tests");
        for (var i = 0; i < 30; i++)
        {
            (await LoginAsync(client, $"spoof-{i}@example.test", $"198.51.100.{i + 1}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var thirtyFirst = await LoginAsync(client, "spoof-30@example.test", "198.51.100.200");

        thirtyFirst.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task Challenge_WhenForwardedHostSent_ShouldRedirectToOwnHost()
    {
        using var client = AuthClient.Create(Host);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/backlog");
        request.Headers.Add("X-Forwarded-Host", "evil.example");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.Host.ShouldBe(new Uri(Host.BaseUrl).Host);
        response.Headers.Location.Port.ShouldBe(new Uri(Host.BaseUrl).Port);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new LoginRequest(email, "wrong-password-1")) };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }

    private static IPAddress OtherLocalAddress() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
        .Select(address => address.Address)
        .First(address => address.AddressFamily == AddressFamily.InterNetwork);
}
