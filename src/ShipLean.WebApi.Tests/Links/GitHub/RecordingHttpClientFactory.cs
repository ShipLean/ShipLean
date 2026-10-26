namespace ShipLean.WebApi.Tests.Links.GitHub;

// An IHttpClientFactory whose clients really call the (loopback, possibly self-signed https) stub server and remember every request sent, headers included.
public sealed class RecordingHttpClientFactory : IHttpClientFactory
{
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public HttpClient CreateClient(string name) => new(new Recorder(_requests, new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }));

    private sealed class Recorder(List<HttpRequestMessage> requests, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(request);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
