using DbUp;
using Npgsql;
using NpgsqlTypes;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Storage;

// Blobs as rows of the blob table (db/blobs) in the database SHIPLEAN_BLOB_DB_CONNECTION names, which may be the app's
// own database or another. Owns its NpgsqlDataSource. A put reads the content into memory first (callers' streams are
// async-only, and Npgsql reads a Stream parameter synchronously), then writes it with one upsert statement, so a content
// stream that fails leaves the previous row. A get buffers the row's bytes (at most AttachmentLimits, 25 MiB by default) into a
// MemoryStream: it is seekable with a Length (Content-Length and ranges), and the connection goes back to the pool at
// once instead of being held until the caller disposes the body.
public sealed class PostgresBlobStore : IBlobStore, IAsyncDisposable, IDisposable
{
    private const string BlobScripts = "db/blobs/";

    private const string PutSql = """
        INSERT INTO blob (key, content_type, bytes, size_bytes)
        VALUES (@key, @contentType, @bytes, octet_length(@bytes))
        ON CONFLICT (key) DO UPDATE
            SET content_type = EXCLUDED.content_type, bytes = EXCLUDED.bytes,
                size_bytes = EXCLUDED.size_bytes, updated_at = now()
        RETURNING size_bytes
        """;

    private readonly NpgsqlDataSource _dataSource;

    // connectionString is already pinned to UTC (DatabaseServices.PinUtc).
    public PostgresBlobStore(string connectionString)
    {
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    // Applies the embedded db/blobs scripts to the blob database, journaled apart from the app's own scripts.
    public static void ApplySchema(string connectionString)
    {
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(PostgresBlobStore).Assembly, name => name.StartsWith(BlobScripts, StringComparison.Ordinal))
            .JournalToPostgresqlTable("public", "blob_schemaversions")
            .LogToNowhere()
            .Build();
        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Blob database upgrade failed at {result.ErrorScript?.Name}: {result.Error?.Message}", result.Error);
        }
    }

    public async Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        BlobKey.Check(key);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        await using var command = _dataSource.CreateCommand(PutSql);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("contentType", contentType);
        command.Parameters.Add(new NpgsqlParameter("bytes", NpgsqlDbType.Bytea) { Value = buffer.ToArray() });
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken)
    {
        BlobKey.Check(key);

        await using var command = _dataSource.CreateCommand("SELECT content_type, bytes FROM blob WHERE key = @key");
        command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BlobContent(new MemoryStream(reader.GetFieldValue<byte[]>(1), writable: false), reader.GetString(0));
    }

    // The rest of the blob streamed from the database: the server cuts it at the offset (substring), and the reader runs in sequential
    // mode, so the bytes are never held whole here. The returned stream owns the connection, command and reader; disposing it
    // gives the connection back to the pool.
    public async Task<Stream?> OpenAtAsync(string key, long offset, CancellationToken cancellationToken)
    {
        BlobKey.Check(key);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        NpgsqlCommand? command = null;
        NpgsqlDataReader? reader = null;
        try
        {
            command = new NpgsqlCommand("SELECT substring(bytes from (@offset + 1)::int) FROM blob WHERE key = @key", connection);
            command.Parameters.AddWithValue("key", key);
            command.Parameters.Add(new NpgsqlParameter("offset", NpgsqlDbType.Bigint) { Value = offset });
            reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await DisposeAllAsync(reader, command, connection);
                return null;
            }

            return new PostgresBlobReadStream(reader.GetStream(0), reader, command, connection);
        }
        catch
        {
            await DisposeAllAsync(reader, command, connection);
            throw;
        }
    }

    private static async Task DisposeAllAsync(NpgsqlDataReader? reader, NpgsqlCommand? command, NpgsqlConnection connection)
    {
        if (reader is not null)
        {
            await reader.DisposeAsync();
        }

        if (command is not null)
        {
            await command.DisposeAsync();
        }

        await connection.DisposeAsync();
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        BlobKey.Check(key);

        await using var command = _dataSource.CreateCommand("SELECT EXISTS (SELECT 1 FROM blob WHERE key = @key)");
        command.Parameters.AddWithValue("key", key);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    public void Dispose() => _dataSource.Dispose();
}
