using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ShipLean.TestSupport.AgentReceiver;

// A stand-in for an agent relay on a real loopback socket at http://127.0.0.1:<free port>/hook: it records every webhook it gets (headers and the
// exact body bytes) and answers Status. Create an agent connection whose receiver URL is Url and the app's own delivery HttpClient reaches it, so an
// acceptance journey can drive a real Test connection ping. Dispose it at teardown.
public sealed class TestAgentReceiver : IAsyncDisposable
{
    private const int BindAttempts = 5;

    private readonly HttpListener _listener;
    private readonly Task _loop;
    private readonly List<ReceivedWebhook> _received = [];
    private int _status;

    public TestAgentReceiver(HttpStatusCode status = HttpStatusCode.OK)
    {
        _status = (int)status;
        (_listener, Url) = Bind();
        _loop = Task.Run(Serve);
    }

    public string Url { get; }

    // A free port is found by probing and releasing it, so another process can take it before the listener binds: on a start failure
    // the next port is tried, a few times.
    private static (HttpListener Listener, string Url) Bind()
    {
        for (var attempt = 1; attempt < BindAttempts; attempt++)
        {
            try
            {
                return TryBind();
            }
            catch (HttpListenerException)
            {
                // the port was taken in between: go round with a new one
            }
        }

        return TryBind();
    }

    private static (HttpListener Listener, string Url) TryBind()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var url = $"http://127.0.0.1:{port}/hook";
        var listener = new HttpListener();
        listener.Prefixes.Add(url + "/");
        try
        {
            listener.Start();
        }
        catch (HttpListenerException)
        {
            listener.Close();
            throw;
        }

        return (listener, url);
    }

    // What the next answers carry; change it between calls.
    public HttpStatusCode Status
    {
        get => (HttpStatusCode)Volatile.Read(ref _status);
        set => Volatile.Write(ref _status, (int)value);
    }

    public IReadOnlyList<ReceivedWebhook> Received
    {
        get
        {
            lock (_received)
            {
                return [.. _received];
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Close();
        await _loop;
    }

    private async Task Serve()
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

            using var buffer = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(buffer);
            var headers = context.Request.Headers.AllKeys.ToDictionary(k => k!, k => context.Request.Headers[k]!, StringComparer.OrdinalIgnoreCase);
            lock (_received)
            {
                _received.Add(new ReceivedWebhook(headers, buffer.ToArray()));
            }

            context.Response.StatusCode = Volatile.Read(ref _status);
            context.Response.Close();
        }
    }

    public sealed record ReceivedWebhook(IReadOnlyDictionary<string, string> Headers, byte[] Body)
    {
        public string Text => Encoding.UTF8.GetString(Body);

        // The way a relay written for Linear checks it: lowercase hex HMAC-SHA256 of the exact body, keyed with the signing secret.
        public bool IsSignedWith(string signingSecret)
            => Headers.TryGetValue("Linear-Signature", out var signature)
               && signature == Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), Body));
    }
}
