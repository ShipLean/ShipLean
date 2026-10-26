using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Features.Import.Jira;

// The primary handler of the named Jira HttpClient. It resolves the host itself and refuses private addresses unless
// they are allowed, so a name that points at the internal network (or is rebound to it between a check and the call)
// never gets a connection. No proxy is used: with one, the connection goes to the proxy and this check would never see the Jira host. Redirects are not followed here: the client follows them itself so it controls the token.
public static class JiraConnector
{
    public static SocketsHttpHandler CreateHandler(IConfiguration configuration) => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(15),
        ConnectCallback = (context, cancellationToken) => ConnectAsync(configuration, context.DnsEndPoint, cancellationToken),
    };

    private static async ValueTask<Stream> ConnectAsync(IConfiguration configuration, DnsEndPoint endpoint, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        if (!JiraEndpointPolicy.AllowsLocal(configuration) && (addresses.Length == 0 || addresses.Any(JiraEndpointPolicy.IsLocal)))
        {
            throw new JiraBlockedAddressException(endpoint.Host);
        }
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, endpoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
