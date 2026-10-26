namespace ShipLean.Contracts.Search;

public interface ISearchRanker
{
    Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);
}
