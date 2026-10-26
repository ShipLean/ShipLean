using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace ShipLean.Application.Http;

// SocketsHttpHandler.ConnectCallback for the metadata-document and public-fetch clients. It resolves the host once, refuses the
// connection (AddressRefusedException) if any answer is not allowed, and connects to that vetted address: there is no second
// lookup an attacker could change (DNS rebinding). TLS still validates the certificate against the original host name.
public sealed class PinnedConnector(IHostResolver resolver, IAddressCheck check)
{
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await resolver.ResolveAsync(host, cancellationToken);
        if (addresses.Count == 0 || addresses.Any(address => !check.IsAllowed(address)))
        {
            throw new AddressRefusedException("The host does not resolve to an allowed address.");
        }

        // Every address was vetted, so any of them may be tried; the first that answers wins.
        Exception? last = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                last = exception;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        throw new HttpRequestException("The host did not answer.", last);
    }
}
