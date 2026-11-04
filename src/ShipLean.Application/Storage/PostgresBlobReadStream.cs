using Npgsql;

namespace ShipLean.Application.Storage;

// A blob's rest streamed from one query. It owns the reader, command and connection behind the stream, so disposing it is what
// gives the pooled connection back; a caller that forgets leaks the connection.
internal sealed class PostgresBlobReadStream(Stream source, NpgsqlDataReader reader, NpgsqlCommand command, NpgsqlConnection connection) : Stream
{
    private bool _disposed;

    public override bool CanRead => !_disposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => source.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void Flush()
    {
        // Read-only: nothing to flush.
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            source.Dispose();
            reader.Dispose();
            command.Dispose();
            connection.Dispose();
        }

        base.Dispose(disposing);
    }
}
