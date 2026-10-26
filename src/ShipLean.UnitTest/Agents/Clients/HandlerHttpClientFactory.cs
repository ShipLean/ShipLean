namespace ShipLean.UnitTest.Agents.Clients;

internal sealed class HandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
