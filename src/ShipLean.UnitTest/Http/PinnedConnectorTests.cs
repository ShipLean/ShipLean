using System.Net;
using System.Net.Sockets;
using ShipLean.Application.Http;

namespace ShipLean.UnitTest.Http;

// The SSRF guard, in the same wiring production uses (SocketsHttpHandler.ConnectCallback), with a resolver we control.
public sealed class PinnedConnectorTests
{
    private sealed class FixedResolver(params string[] addresses) : IHostResolver
    {
        public int Lookups { get; private set; }

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult<IReadOnlyList<IPAddress>>(addresses.Select(IPAddress.Parse).ToArray());
        }
    }

    private sealed class AllowAll : IAddressCheck
    {
        public bool IsAllowed(IPAddress address) => true;
    }

    private static HttpClient ClientFor(IHostResolver resolver, IAddressCheck check)
        => new(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = new PinnedConnector(resolver, check).ConnectAsync,
        });

    [Test]
    public async Task Cimd_WhenHostResolvesPrivate_ShouldRefuse()
    {
        var resolver = new FixedResolver("10.0.0.5");
        using var client = ClientFor(resolver, new PublicAddressCheck());

        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://client.example.test/oauth/metadata.json"));

        resolver.Lookups.ShouldBe(1);
    }

    [Test]
    public async Task Cimd_WhenAnyAnswerIsPrivate_ShouldRefuse()
    {
        using var client = ClientFor(new FixedResolver("93.184.216.34", "127.0.0.1"), new PublicAddressCheck());

        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://client.example.test/oauth/metadata.json"));
    }

    [Test]
    public async Task Connect_WhenHostIsPrivateLiteral_ShouldRefuseWithoutLookup()
    {
        var resolver = new FixedResolver("93.184.216.34");
        using var client = ClientFor(resolver, new PublicAddressCheck());

        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://169.254.169.254/latest/meta-data"));

        resolver.Lookups.ShouldBe(0);
    }

    [Test]
    public async Task Connect_WhenAllowed_ShouldConnectToTheVettedAddressAfterOneLookup()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var resolver = new FixedResolver("127.0.0.1");
        using var client = ClientFor(resolver, new AllowAll());

        var call = client.GetAsync($"https://client.example.test:{port}/oauth/metadata.json");
        using var accepted = await listener.AcceptTcpClientAsync();

        accepted.Connected.ShouldBeTrue();
        resolver.Lookups.ShouldBe(1);
        accepted.Dispose();
        await Should.ThrowAsync<HttpRequestException>(() => call);
    }

    [Test]
    public async Task Connect_WhenFirstAddressRefusesConnection_ShouldTryTheNext()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = ClientFor(new FixedResolver("::1", "127.0.0.1"), new AllowAll());

        var call = client.GetAsync($"https://client.example.test:{port}/oauth/metadata.json");
        using var accepted = await listener.AcceptTcpClientAsync();

        accepted.Connected.ShouldBeTrue();
        accepted.Dispose();
        await Should.ThrowAsync<HttpRequestException>(() => call);
    }
}
