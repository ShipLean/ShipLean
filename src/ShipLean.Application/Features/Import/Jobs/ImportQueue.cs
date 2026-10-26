using System.Collections.Concurrent;
using System.Threading.Channels;
using Agoda.IoC.Core;

namespace ShipLean.Application.Features.Import.Jobs;

// The in-process hand-off between the requests that queue an import and the runner that works through them, one at a time, in the order queued.
// A stop request for a job that is running lives here too: the runner looks at it between batches.
[RegisterSingleton]
public sealed class ImportQueue
{
    private readonly Channel<Guid> _jobs = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, bool> _stops = new();

    public void Enqueue(Guid jobId) => _jobs.Writer.TryWrite(jobId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) => _jobs.Reader.ReadAllAsync(cancellationToken);

    public void RequestStop(Guid jobId) => _stops[jobId] = true;

    public bool IsStopRequested(Guid jobId) => _stops.ContainsKey(jobId);

    public void ClearStop(Guid jobId) => _stops.TryRemove(jobId, out _);
}
