using ShipLean.Contracts.Search;

namespace ShipLean.TestSupport.Search;

// ISearchIndexer that runs no pass: it records each call's full flag and returns the counts a test sets.
public sealed class RecordingSearchIndexer : ISearchIndexer
{
    private readonly List<bool> _calls = [];

    public IndexPassResult Result { get; set; } = new(0, 0, 0);

    public IReadOnlyList<bool> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public Task<IndexPassResult> RunPassAsync(bool full, CancellationToken cancellationToken = default)
    {
        lock (_calls)
        {
            _calls.Add(full);
        }
        return Task.FromResult(Result);
    }
}
