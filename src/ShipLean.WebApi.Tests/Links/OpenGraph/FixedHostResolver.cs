using System.Net;
using System.Net.Sockets;
using ShipLean.Application.Http;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// DNS for tests: each name answers the addresses it was given, and any other name is not found. No lookup leaves the process.
public sealed class FixedHostResolver(IReadOnlyDictionary<string, string> answers) : IHostResolver
{
    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        => answers.TryGetValue(host, out var address)
            ? Task.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse(address)])
            : throw new SocketException((int)SocketError.HostNotFound);
}
