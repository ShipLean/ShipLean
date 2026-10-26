namespace ShipLean.WebApi.Tests.Import.Jira;

// Answers every request with whatever the function says (given the request and its body), and remembers what was asked.
internal sealed class FuncHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly List<string> _requests = [];

    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _requests.Add($"{request.Method} {request.RequestUri}");
        }
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        return respond(request, body);
    }
}
