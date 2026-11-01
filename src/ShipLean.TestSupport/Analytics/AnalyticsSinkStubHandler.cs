using System.Net;
using System.Text;

namespace ShipLean.TestSupport.Analytics;

// An HttpMessageHandler standing in for Umami or PostHog: records every request and answers the configured status and body.
// A real sink is built on HttpClient(new AnalyticsSinkStubHandler()).
public sealed class AnalyticsSinkStubHandler : HttpMessageHandler
{
    public sealed record Request(HttpMethod Method, Uri Url, IReadOnlyDictionary<string, string> Headers, string Body);

    private readonly object _gate = new();
    private readonly List<Request> _requests = [];
    private HttpStatusCode _status = HttpStatusCode.OK;
    private string _responseBody = "{}";

    public IReadOnlyList<Request> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    // The answer to every following request.
    public void Respond(HttpStatusCode status, string body = "{}")
    {
        lock (_gate)
        {
            _status = status;
            _responseBody = body;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
        HttpStatusCode status;
        string responseBody;
        lock (_gate)
        {
            _requests.Add(new Request(request.Method, request.RequestUri!, headers, body));
            status = _status;
            responseBody = _responseBody;
        }
        return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json"), RequestMessage = request };
    }
}
