using System.Net;
using System.Net.Sockets;
using ShipLean.Application.Http;

namespace ShipLean.TestSupport.Http;

// An IHostResolver that never leaves the process: an IP literal resolves to itself, "localhost" to loopback, and any other
// name fails as an unknown host. Every test host registers it, so a test that adds a link never does a real DNS lookup.
public sealed class NoNetworkHostResolver : IHostResolver
{
    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return Task.FromResult<IReadOnlyList<IPAddress>>([literal]);
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
        }

        throw new SocketException((int)SocketError.HostNotFound);
    }
}
