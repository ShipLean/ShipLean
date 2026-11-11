using ShipLean.Application.Features.WorkItemDetails.Attachments;

namespace ShipLean.TestSupport.Backups;

// Yields one byte, then waits to be released before ending; Paused completes once the reader is waiting.
internal sealed class GatedStream : ForwardOnlyStream
{
    private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _gaveByte;

    public Task Paused => _paused.Task;

    public void Release() => _release.TrySetResult();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_gaveByte)
        {
            _gaveByte = true;
            buffer.Span[0] = (byte)'y';
            return 1;
        }

        _paused.TrySetResult();
        await _release.Task;
        return 0;
    }
}
