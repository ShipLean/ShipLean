using System.Net;
using System.Net.Sockets;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// A loopback port that accepts connections and never answers: what a hung host looks like to a fetch.
public sealed class SilentServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    public SilentServer()
    {
        _listener.Start();
    }

    public Uri UrlOf(string path) => new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}{path}");

    public void Dispose() => _listener.Dispose();
}
