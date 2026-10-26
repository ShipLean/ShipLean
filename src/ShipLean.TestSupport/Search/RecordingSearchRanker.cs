using ShipLean.Contracts.Search;

namespace ShipLean.TestSupport.Search;

// ISearchRanker that searches nothing: it records each query and returns the results a test sets (none by default).
public sealed class RecordingSearchRanker : ISearchRanker
{
    private readonly List<SearchQuery> _queries = [];

    public SearchResults Results { get; set; } = new(false, [], [], [], false);

    public IReadOnlyList<SearchQuery> Queries
    {
        get
        {
            lock (_queries)
            {
                return [.. _queries];
            }
        }
    }

    public Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        lock (_queries)
        {
            _queries.Add(query);
        }
        return Task.FromResult(Results);
    }
}
