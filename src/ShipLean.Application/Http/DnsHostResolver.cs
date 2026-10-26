using System.Net;

namespace ShipLean.Application.Http;

public sealed class DnsHostResolver : IHostResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        => await Dns.GetHostAddressesAsync(host, cancellationToken);
}
