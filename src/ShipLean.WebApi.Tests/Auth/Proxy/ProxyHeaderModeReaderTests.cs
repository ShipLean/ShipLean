using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Plain-header mode from loopback, the proxy on the same box. No identity provider: the headers are the identity.
[TestFixture]
public class ProxyHeaderModeReaderTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override Action<IServiceCollection>? ConfigureProxyTestServices => ProxyIdentityProbeController.AddTo;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => TestIdentityProvider.HeaderSettings();

    [Test]
    public async Task Read_WhenHeaderModeFromLoopback_ShouldReturnIdentity()
    {
        using var client = ProxyClient().WithHeaders(("Remote-Email", "remote@example.test"), ("Remote-Name", "Re Mote"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("remote@example.test", "Re Mote"));
    }

    // The trusted loopback proxy names its client; UseForwardedHeaders then makes that client the connection's address,
    // so this passes only because the reader trusts the raw peer captured before it.
    [Test]
    public async Task Read_WhenLoopbackProxyForwardsClientAddress_ShouldReturnIdentity()
    {
        using var client = ProxyClient().WithHeaders(("X-Forwarded-For", "203.0.113.9"), ("Remote-Email", "forwarded@example.test"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("forwarded@example.test", null));
    }

    [Test]
    public async Task Read_WhenNameHeaderMissing_ShouldReturnNullName()
    {
        using var client = ProxyClient().WithHeaders(("Remote-Email", "nameless@example.test"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBe(new ProxyIdentity("nameless@example.test", null));
    }

    [Test]
    public async Task Read_WhenEmailHeaderInvalid_ShouldReturnNone()
    {
        using var client = ProxyClient().WithHeaders(("Remote-Email", "not-an-email"), ("Remote-Name", "Bad"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }

    // Two Remote-Email lines (HttpClient would fold them into one), so the request goes over a raw socket.
    [Test]
    public async Task Read_WhenEmailHeaderDuplicated_ShouldReturnNone()
    {
        var baseUri = new Uri(Host.BaseUrl);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(baseUri.Host, baseUri.Port);
        await using var stream = tcp.GetStream();
        var request = $"GET {ProxyIdentityProbeController.Path} HTTP/1.1\r\nHost: {baseUri.Authority}\r\n"
                      + "Remote-Email: first@example.test\r\nRemote-Email: second@example.test\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var response = await reader.ReadToEndAsync();

        response.ShouldStartWith("HTTP/1.1 200");
        response.ShouldContain("{\"identity\":null}");
    }

    [Test]
    public async Task Read_WhenNoEmailHeader_ShouldReturnNone()
    {
        using var client = ProxyClient().WithHeaders(("Remote-Name", "Only A Name"));

        (await ProxyIdentityProbeController.ReadAsync(client)).ShouldBeNull();
    }
}
