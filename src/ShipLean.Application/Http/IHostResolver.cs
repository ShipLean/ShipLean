using System.Net;

namespace ShipLean.Application.Http;

// The one DNS lookup behind a metadata-document fetch. Swappable so tests can point a name at any address.
public interface IHostResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}
