namespace ShipLean.Application.Features.Search.Ranking.Fusion;

// One item after fusion. Best is the hit that supplies MatchedIn, Snippet and Attachment (see ReciprocalRankFusion).
// Score is the RRF sum until the boosts multiply it.
public sealed class FusedItem(CandidateHit best, int bestRank)
{
    public CandidateHit Best { get; set; } = best;

    public int BestRank { get; set; } = bestRank;

    public double Score { get; set; }
}
