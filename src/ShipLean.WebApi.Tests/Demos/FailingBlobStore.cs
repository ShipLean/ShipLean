using ShipLean.Contracts.Blobs;

namespace ShipLean.WebApi.Tests.Demos;

// The real blob store, throwing on the Nth PutAsync counted from FailOnPut (0 = never). The failed put writes nothing, as an IBlobStore failure is all or nothing.
internal sealed class FailingBlobStore(IBlobStore inner) : IBlobStore
{
    private int _puts;
    private int _failOn;

    public void FailOnPut(int nth)
    {
        Volatile.Write(ref _puts, 0);
        Volatile.Write(ref _failOn, nth);
    }

    public void Heal() => Volatile.Write(ref _failOn, 0);

    public async Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var put = Interlocked.Increment(ref _puts);
        if (put == Volatile.Read(ref _failOn))
        {
            throw new IOException($"The blob store failed on put {put}.");
        }

        return await inner.PutAsync(key, content, contentType, cancellationToken);
    }

    public Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken) => inner.GetAsync(key, cancellationToken);

    public Task<Stream?> OpenAtAsync(string key, long offset, CancellationToken cancellationToken) => inner.OpenAtAsync(key, offset, cancellationToken);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) => inner.ExistsAsync(key, cancellationToken);
}
