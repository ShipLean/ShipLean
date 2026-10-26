namespace ShipLean.Application.Features.Search.Ranking;

// One leg of the ranker. Registered once per leg (several implementations).
public interface IItemCandidates
{
    CandidateLeg Leg { get; }

    Task<CandidateLists> ListsAsync(CandidateQuery query, CancellationToken cancellationToken = default);
}
