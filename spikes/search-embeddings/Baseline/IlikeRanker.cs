using System.Diagnostics;
using SearchEmbeddings.Corpus;

namespace SearchEmbeddings.Baseline;

// Today's search (OrientReads.SearchAsync) without its attachment leg: an item matches when its title or description
// contains any query word (ILIKE %word%), Removed items are excluded, and hits sort by WordOverlap on the title, then
// most recently changed first (later in the corpus file = newer).
public sealed class IlikeRanker : IRanker
{
    private const int Cap = 25;

    public string Label => "ilike";

    public Rankings Rank(IReadOnlyList<CorpusFile> corpus, IReadOnlyList<EvalQuery> queries, RankerOptions options)
    {
        var items = corpus.SelectMany(f => f.Items).Where(i => i.State != "Removed").Reverse().ToList();
        var rankings = new Rankings(Label, [], []);
        foreach (var query in queries)
        {
            var clock = Stopwatch.StartNew();
            var words = query.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var hits = items
                .Where(i => words.Any(w => i.Title.Contains(w, StringComparison.OrdinalIgnoreCase) || i.Description.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(i => WordOverlap.Score(i.Title, words))
                .Take(Cap)
                .Select(i => new RankedRef(i.Ref, null))
                .ToList();
            rankings.Queries[query.Id] = hits;
            rankings.LatencyMs[query.Id] = clock.Elapsed.TotalMilliseconds;
        }
        return rankings;
    }
}
