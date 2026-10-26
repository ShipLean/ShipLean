namespace ShipLean.AcceptanceTests.Lanes.Import.Run;

// While closed, Jira answers every attachment download with 404, so the import records an error for each attachment.
internal sealed class AttachmentGate : DelegatingHandler
{
    private volatile bool _closed;

    public bool Closed
    {
        get => _closed;
        set => _closed = value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        _closed && request.RequestUri!.AbsolutePath.Contains("/attachment/", StringComparison.Ordinal)
            ? Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound))
            : base.SendAsync(request, cancellationToken);
}
