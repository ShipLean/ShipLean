namespace ShipLean.Application.Features.Search.Ranking;

// Available false means the leg can't run (no embedder). PendingRows is how many rows of the matching items are not
// embedded yet, so the ranker knows whether the semantic leg saw everything.
public sealed record CandidateLists(bool Available, int PendingRows, IReadOnlyList<RankedList> Lists);
