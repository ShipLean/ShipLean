namespace ShipLean.WebApi.Tests.AgentDispatch;

internal sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
