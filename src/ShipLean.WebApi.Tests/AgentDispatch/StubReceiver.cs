using System.Net;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// Stands in for the agent: records every request it gets (with the raw body bytes) and answers with the next scripted status
// (200 once the script is used up), or throws what the script holds.
internal sealed class StubReceiver : HttpMessageHandler
{
    private readonly List<Received> _requests = [];
    private readonly Queue<object> _script = new();

    public IReadOnlyList<Received> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    // A status code, or an exception to throw.
    public StubReceiver Then(params object[] outcomes)
    {
        foreach (var outcome in outcomes)
        {
            _script.Enqueue(outcome);
        }

        return this;
    }

    public HttpClient Client() => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
        var headers = request.Headers.Concat(request.Content.Headers).ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);
        lock (_requests)
        {
            _requests.Add(new Received(request.RequestUri!, headers, body));
        }

        object outcome;
        lock (_script)
        {
            outcome = _script.Count > 0 ? _script.Dequeue() : HttpStatusCode.OK;
        }

        return outcome is Exception failure ? throw failure : new HttpResponseMessage((HttpStatusCode)outcome);
    }

    internal sealed record Received(Uri Url, IReadOnlyDictionary<string, string> Headers, byte[] Body);
}
