using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Endpoint;

// Groups that return the project and sprint they were given, whatever the query.
public sealed class StubSearchGroups : ISearchGroups
{
    public IReadOnlyList<SearchResultProject> Projects { get; set; } = [];

    public IReadOnlyList<SearchResultSprint> Sprints { get; set; } = [];

    public Task<IReadOnlyList<SearchResultProject>> ProjectsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(Projects);

    public Task<IReadOnlyList<SearchResultSprint>> SprintsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(Sprints);
}
