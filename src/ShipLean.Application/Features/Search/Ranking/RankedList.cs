namespace ShipLean.Application.Features.Search.Ranking;

// Hits come best first, at most SearchOptions.TopPerList of them, one per item.
public sealed record RankedList(RankedListName Name, IReadOnlyList<CandidateHit> Hits);
