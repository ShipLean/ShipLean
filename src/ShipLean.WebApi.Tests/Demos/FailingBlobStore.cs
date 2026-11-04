using ShipLean.Contracts.Blobs;

namespace ShipLean.WebApi.Tests.Demos;

// The real blob store, throwing on the put whose content type was named in FailOnContentType (null = never). The failed put writes nothing, as an IBlobStore failure is all or nothing.
internal sealed class FailingBlobStore(IBlobStore inner) : IBlobStore
{
    private string? _failOn;

    public void FailOnContentType(string contentType) => Volatile.Write(ref _failOn, contentType);

    public void Heal() => Volatile.Write(ref _failOn, null);

    public async Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (contentType == Volatile.Read(ref _failOn))
        {
            throw new IOException($"The blob store failed on {contentType}.");
        }

        return await inner.PutAsync(key, content, contentType, cancellationToken);
    }

    public Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken) => inner.GetAsync(key, cancellationToken);

    public Task<Stream?> OpenAtAsync(string key, long offset, CancellationToken cancellationToken) => inner.OpenAtAsync(key, offset, cancellationToken);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) => inner.ExistsAsync(key, cancellationToken);
}
