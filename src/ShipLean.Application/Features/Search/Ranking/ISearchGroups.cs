using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking;

// The project and sprint matches shown beside the items.
public interface ISearchGroups
{
    Task<IReadOnlyList<SearchResultProject>> ProjectsAsync(CandidateQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchResultSprint>> SprintsAsync(CandidateQuery query, CancellationToken cancellationToken = default);
}
