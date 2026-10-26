using System.Threading.Channels;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Search;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

// A RecordingSearchIndexer a test can await: each pass is queued when it starts. A test can make the next passes throw, or hold
// a pass until it is cancelled (a stop must cancel it and wait).
public sealed class ObservedSearchIndexer : ISearchIndexer
{
    private readonly RecordingSearchIndexer _inner = new();
    private readonly Channel<bool> _passes = Channel.CreateUnbounded<bool>();
    private int _failures;

    public IReadOnlyList<bool> Calls => _inner.Calls;

    public bool HoldUntilCancelled { get; set; }

    public bool Cancelled { get; private set; }

    public bool Finished { get; private set; }

    public void FailNext(int count) => Volatile.Write(ref _failures, count);

    public async Task<bool> NextPassAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await _passes.Reader.ReadAsync(deadline.Token);
    }

    public async Task<IndexPassResult> RunPassAsync(bool full, CancellationToken cancellationToken = default)
    {
        var result = await _inner.RunPassAsync(full, cancellationToken);
        _passes.Writer.TryWrite(full);
        if (Interlocked.Decrement(ref _failures) >= 0)
        {
            throw new InvalidOperationException("The pass failed on purpose.");
        }

        if (HoldUntilCancelled)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled = true;
                await Task.Yield();
                Finished = true;
                throw;
            }
        }

        return result;
    }
}
