using System.Net.Http;
using ShipLean.Application.Features.Import.Jira;

namespace ShipLean.WebApi.Tests.Import.Jira;

// A body that sends its head and then fails the way a connection closed mid-answer does (an HttpIOException, which is an IOException and not an HttpRequestException).
internal sealed class DroppedStream(byte[] head) : AsyncReadStream
{
    private bool _sent;

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_sent)
        {
            throw new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.");
        }
        _sent = true;
        head.CopyTo(buffer);
        return ValueTask.FromResult(head.Length);
    }
}
