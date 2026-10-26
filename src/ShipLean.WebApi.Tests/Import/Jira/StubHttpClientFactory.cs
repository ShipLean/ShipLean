namespace ShipLean.WebApi.Tests.Import.Jira;

// Hands out clients over one handler (the in-process Jira stub), never disposing it. The timeout is the client's, as in the app.
internal sealed class StubHttpClientFactory(HttpMessageHandler handler, TimeSpan? timeout = null) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = timeout ?? TimeSpan.FromSeconds(60) };
}
