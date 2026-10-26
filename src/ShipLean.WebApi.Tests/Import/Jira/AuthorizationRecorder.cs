namespace ShipLean.WebApi.Tests.Import.Jira;

// Remembers the Authorization header each request carried, by host, then passes the request on.
internal sealed class AuthorizationRecorder(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private readonly object _gate = new();
    private readonly List<(string Host, string? Authorization)> _seen = [];

    public IReadOnlyList<(string Host, string? Authorization)> Seen
    {
        get
        {
            lock (_gate)
            {
                return [.. _seen];
            }
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _seen.Add((request.RequestUri!.Host, request.Headers.Authorization?.ToString()));
        }
        return base.SendAsync(request, cancellationToken);
    }
}
