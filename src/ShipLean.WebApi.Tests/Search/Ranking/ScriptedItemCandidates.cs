using ShipLean.Application.Features.Search.Ranking;

namespace ShipLean.WebApi.Tests.Search.Ranking;

// A leg that returns what the test scripted, including Available and PendingRows, whatever the query.
public sealed class ScriptedItemCandidates(CandidateLeg leg) : IItemCandidates
{
    public CandidateLeg Leg => leg;

    public bool Available { get; set; } = true;

    public int PendingRows { get; set; }

    public IReadOnlyList<RankedList> Lists { get; set; } = [];

    public Task<CandidateLists> ListsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(new CandidateLists(Available, PendingRows, Lists));
}
