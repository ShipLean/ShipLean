using Npgsql;
using ShipLean.Contracts.Blobs;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// An IBlobStore that, inside each put, tries to take a connection from a data source whose pool holds one and times out fast:
// ConnectionFreeDuringPut is false when the caller still had a connection checked out while the bytes were written.
internal sealed class BusyCountingBlobStore(IBlobStore inner, NpgsqlDataSource singleConnectionDataSource) : IBlobStore
{
    public bool? ConnectionFreeDuringPut { get; private set; }

    public async Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await singleConnectionDataSource.OpenConnectionAsync(cancellationToken);
            ConnectionFreeDuringPut = true;
        }
        catch (NpgsqlException)
        {
            ConnectionFreeDuringPut = false;
        }

        return await inner.PutAsync(key, content, contentType, cancellationToken);
    }

    public Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken) => inner.GetAsync(key, cancellationToken);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) => inner.ExistsAsync(key, cancellationToken);
}
