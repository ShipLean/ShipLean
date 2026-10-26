using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// Reads the inner stream and throws ApiException.Validation as soon as more than the limit has been read, so an oversize
// file is never stored and a hostile source cannot fill the disk.
internal sealed class CountingLimitStream(Stream inner, long maxBytes) : ForwardOnlyStream
{
    private long _read;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => Count(await inner.ReadAsync(buffer, cancellationToken));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private int Count(int read)
    {
        _read += read;
        if (_read > maxBytes)
        {
            throw ApiException.Validation($"An attachment can be at most {maxBytes / (1024 * 1024)} MB.");
        }

        return read;
    }
}
