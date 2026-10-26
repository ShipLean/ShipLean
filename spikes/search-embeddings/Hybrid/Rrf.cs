using SearchEmbeddings;

namespace SearchEmbeddings.Hybrid;

// Reciprocal rank fusion as SearchRanker does it: score = sum of 1 / (k + rank + 1), rank 0-based per list.
public static class Rrf
{
    public static List<RankedRef> Fuse(IEnumerable<IReadOnlyList<string>> lists, int k)
    {
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var list in lists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                if (!scores.TryGetValue(list[rank], out var s))
                {
                    order.Add(list[rank]);
                }
                scores[list[rank]] = s + 1.0 / (k + rank + 1);
            }
        }
        return [.. order.Select((r, i) => (r, i)).OrderByDescending(x => scores[x.r]).ThenBy(x => x.i).Select(x => new RankedRef(x.r, scores[x.r]))];
    }
}
