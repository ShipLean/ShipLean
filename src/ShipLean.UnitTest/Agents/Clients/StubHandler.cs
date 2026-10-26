namespace ShipLean.UnitTest.Agents.Clients;

// The network edge of the fetcher: whatever the test says a server answered. Counts the requests it saw.
internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        return respond(request, cancellationToken);
    }
}
