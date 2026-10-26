using System.Net;
using System.Net.Sockets;

namespace ShipLean.WebApi.Tests.Import.Jira;

internal static class LoopbackPorts
{
    // A loopback port nothing listens on, so a connection to it is refused at once.
    public static int Closed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
