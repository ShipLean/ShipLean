using System.Collections.Concurrent;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Fakes.Blobs;

// A dictionary-backed IBlobStore with no I/O. Not registered by any module: BlobStoreServices registers the real stores.
public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _blobs = new(StringComparer.Ordinal);

    public async Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        Check(key);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _blobs[key] = (buffer.ToArray(), contentType);
        return buffer.Length;
    }

    public Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken)
    {
        Check(key);
        return Task.FromResult(_blobs.TryGetValue(key, out var blob)
            ? new BlobContent(new MemoryStream(blob.Bytes), blob.ContentType)
            : null);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        Check(key);
        return Task.FromResult(_blobs.ContainsKey(key));
    }

    private static void Check(string key) => BlobKey.Check(key);
}
