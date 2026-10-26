using System.Net;
using System.Net.Sockets;
using ShipLean.TestSupport.Http;
using Shouldly;

namespace ShipLean.UnitTest.Http;

public sealed class NoNetworkHostResolverTests
{
    private readonly NoNetworkHostResolver _resolver = new();

    [Test]
    public async Task ResolveAsync_WhenHostIsAnIpLiteral_ShouldReturnThatAddress()
    {
        var addresses = await _resolver.ResolveAsync("10.1.2.3", CancellationToken.None);

        addresses.ShouldBe([IPAddress.Parse("10.1.2.3")]);
    }

    [Test]
    public async Task ResolveAsync_WhenHostIsLocalhost_ShouldReturnLoopback()
    {
        var addresses = await _resolver.ResolveAsync("localhost", CancellationToken.None);

        addresses.ShouldBe([IPAddress.Loopback]);
    }

    [Test]
    public void ResolveAsync_WhenHostIsAnyOtherName_ShouldThrowHostNotFound()
    {
        var thrown = Should.Throw<SocketException>(() => _resolver.ResolveAsync("example.com", CancellationToken.None));

        thrown.SocketErrorCode.ShouldBe(SocketError.HostNotFound);
    }
}
