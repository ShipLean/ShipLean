namespace ShipLean.Application.Features.Search.Ranking.Fusion;

// Reciprocal rank fusion: an item's score is the sum of 1 / (k + rank + 1) over every list it is in, rank counted from 0.
// Best hit tie rule: the hit with the lowest rank wins; on equal ranks the list that comes first in RankedListName
// wins (lexical title, lexical body, vector title, vector body, any-word lexical body), whatever order the legs were registered in.
public static class ReciprocalRankFusion
{
    public static IReadOnlyDictionary<Guid, FusedItem> Fuse(IEnumerable<RankedList> lists, int k)
    {
        var fused = new Dictionary<Guid, FusedItem>();
        foreach (var hits in lists.OrderBy(l => l.Name).Select(l => l.Hits))
        {
            for (var rank = 0; rank < hits.Count; rank++)
            {
                var hit = hits[rank];
                if (!fused.TryGetValue(hit.WorkItemId, out var item))
                {
                    fused[hit.WorkItemId] = item = new FusedItem(hit, rank);
                }
                else if (rank < item.BestRank)
                {
                    item.Best = hit;
                    item.BestRank = rank;
                }

                item.Score += 1.0 / (k + rank + 1);
            }
        }

        return fused;
    }
}
