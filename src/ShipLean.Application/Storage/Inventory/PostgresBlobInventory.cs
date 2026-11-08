using Dapper;
using Npgsql;
using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.Application.Storage.Inventory;

// The keys of a PostgresBlobStore: the key column of the blob table, streamed row by row (Dapper unbuffered), so a store
// with millions of blobs is never held in memory. Owns its NpgsqlDataSource. connectionString is already pinned to UTC.
public sealed class PostgresBlobInventory(string connectionString) : IBlobInventory, IAsyncDisposable, IDisposable
{
    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(connectionString);

    public async IAsyncEnumerable<string> ListKeysAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await foreach (var key in connection.QueryUnbufferedAsync<string>("SELECT key FROM blob"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return key;
        }
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    public void Dispose() => _dataSource.Dispose();
}
