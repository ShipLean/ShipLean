using ShipLean.Application.Features.Import.Jira;

namespace ShipLean.WebApi.Tests.Import.Jira;

// A body made up as it is read: the head, then Count filler bytes, then the tail. Each read after the head waits Gap first (honouring cancellation),
// which is how a site that drips or stalls looks; a zero gap serves as fast as it is asked.
internal sealed class GeneratedStream(byte[] head, long count, byte filler, byte[] tail, TimeSpan gap) : AsyncReadStream
{
    private long _position;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position >= head.Length && gap > TimeSpan.Zero)
        {
            await Task.Delay(gap, cancellationToken);
        }
        var offset = _position;
        var size = (int)Math.Min(buffer.Length, gap > TimeSpan.Zero && offset >= head.Length ? 1 : long.MaxValue);
        var written = 0;
        while (written < size && Next(offset + written) is { } next)
        {
            buffer.Span[written++] = next;
        }
        _position += written;
        return written;
    }

    private byte? Next(long at)
    {
        if (at < head.Length)
        {
            return head[at];
        }
        at -= head.Length;
        if (at < count)
        {
            return filler;
        }
        at -= count;
        return at < tail.Length ? tail[at] : null;
    }
}
