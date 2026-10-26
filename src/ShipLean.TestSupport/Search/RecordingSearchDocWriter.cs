using ShipLean.Contracts.Search;

namespace ShipLean.TestSupport.Search;

// ISearchDocWriter that writes no rows: it records the sources of each SyncAsync and counts SyncAllAsync calls.
public sealed class RecordingSearchDocWriter : ISearchDocWriter
{
    private readonly List<IReadOnlyList<SearchSource>> _syncs = [];
    private int _syncAllCalls;

    public IReadOnlyList<IReadOnlyList<SearchSource>> Syncs
    {
        get
        {
            lock (_syncs)
            {
                return [.. _syncs];
            }
        }
    }

    public int SyncAllCalls => Volatile.Read(ref _syncAllCalls);

    public Task SyncAsync(IReadOnlyList<SearchSource> sources, CancellationToken cancellationToken = default)
    {
        lock (_syncs)
        {
            _syncs.Add([.. sources]);
        }
        return Task.CompletedTask;
    }

    public Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _syncAllCalls);
        return Task.CompletedTask;
    }
}
