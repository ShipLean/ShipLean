namespace ShipLean.Contracts.Search;

// One synchronous indexing pass. full also re-syncs every source before embedding.
public interface ISearchIndexer
{
    Task<IndexPassResult> RunPassAsync(bool full, CancellationToken cancellationToken = default);
}
