using System.Threading.Channels;

namespace ShipLean.Application.Features.Backups.Schedule;

// The hand-off from BackupTrigger to BackupWorker: started runs, in the order they started. Only one run holds the lease
// at a time, so it rarely holds more than one.
public sealed class BackupQueue
{
    private readonly Channel<BackupRunRequest> _runs = Channel.CreateUnbounded<BackupRunRequest>(new UnboundedChannelOptions { SingleReader = true });

    // False only once the queue is closed (the worker has stopped).
    public bool TryEnqueue(BackupRunRequest request) => _runs.Writer.TryWrite(request);

    public ValueTask<BackupRunRequest> ReadAsync(CancellationToken cancellationToken) => _runs.Reader.ReadAsync(cancellationToken);

    public bool TryRead(out BackupRunRequest request)
    {
        if (_runs.Reader.TryRead(out var read))
        {
            request = read;
            return true;
        }
        request = null!;
        return false;
    }

    // No more runs are accepted: TryEnqueue returns false from now on.
    public void Close() => _runs.Writer.TryComplete();
}
