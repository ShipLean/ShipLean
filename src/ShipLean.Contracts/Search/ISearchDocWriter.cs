namespace ShipLean.Contracts.Search;

// Writes the text rows of search_doc and drops the rows of removed or archived sources. Changed text clears the embedding.
public interface ISearchDocWriter
{
    Task SyncAsync(IReadOnlyList<SearchSource> sources, CancellationToken cancellationToken = default);

    Task SyncAllAsync(CancellationToken cancellationToken = default);
}
