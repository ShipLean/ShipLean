using System.Threading.Channels;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Search;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

// A RecordingSearchDocWriter a test can await: each SyncAsync is also queued, so the test reads the next one instead of sleeping.
public sealed class ObservedSearchDocWriter : ISearchDocWriter
{
    private readonly RecordingSearchDocWriter _inner = new();
    private readonly Channel<IReadOnlyList<SearchSource>> _syncs = Channel.CreateUnbounded<IReadOnlyList<SearchSource>>();

    private int _failures;

    public int SyncAllCalls => _inner.SyncAllCalls;

    // The next count syncs throw (each is still queued for NextSyncAsync); int.MaxValue = until FailNext(0).
    public void FailNext(int count) => Volatile.Write(ref _failures, count);

    public void FailAlways() => FailNext(int.MaxValue);

    public async Task<IReadOnlyList<SearchSource>> NextSyncAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await _syncs.Reader.ReadAsync(deadline.Token);
    }

    public async Task SyncAsync(IReadOnlyList<SearchSource> sources, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _failures) > 0)
        {
            if (Volatile.Read(ref _failures) != int.MaxValue)
            {
                Interlocked.Decrement(ref _failures);
            }

            _syncs.Writer.TryWrite([.. sources]);
            throw new InvalidOperationException("The sync failed on purpose.");
        }

        await _inner.SyncAsync(sources, cancellationToken);
        _syncs.Writer.TryWrite([.. sources]);
    }

    public Task SyncAllAsync(CancellationToken cancellationToken = default) => _inner.SyncAllAsync(cancellationToken);
}
