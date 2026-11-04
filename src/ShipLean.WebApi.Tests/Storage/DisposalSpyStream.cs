namespace ShipLean.WebApi.Tests.Storage;

// A response stream that records whether it was disposed, standing in for the HTTP body of an S3 GetObject response.
public sealed class DisposalSpyStream(byte[] bytes) : MemoryStream(bytes)
{
    public bool WasDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
