using System.Net;
using System.Net.Sockets;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// A receiver on a real loopback socket, for the tests that need the app's own HttpClient (redirects, headers on the wire).
internal sealed class LocalReceiver : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Task _loop;
    private int _hits;

    public LocalReceiver(HttpStatusCode answer, string? location = null)
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Url = $"http://127.0.0.1:{port}/hook/";
        _listener.Prefixes.Add(Url);
        _listener.Start();
        _loop = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception failed) when (failed is HttpListenerException or ObjectDisposedException)
                {
                    return;
                }

                Interlocked.Increment(ref _hits);
                LastHeaders = context.Request.Headers.AllKeys.ToDictionary(k => k!, k => context.Request.Headers[k]!, StringComparer.OrdinalIgnoreCase);
                context.Response.StatusCode = (int)answer;
                if (location is not null)
                {
                    context.Response.RedirectLocation = location;
                }

                context.Response.Close();
            }
        });
    }

    public string Url { get; }

    public int Hits => Volatile.Read(ref _hits);

    public IReadOnlyDictionary<string, string> LastHeaders { get; private set; } = new Dictionary<string, string>();

    public async ValueTask DisposeAsync()
    {
        _listener.Close();
        await _loop;
    }
}
