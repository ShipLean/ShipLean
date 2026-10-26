using System.Diagnostics;
using SearchEmbeddings.Corpus;

namespace SearchEmbeddings.Hybrid;

// --ranker hybrid --inputs a.json,b.json: RRF (k=60) over the rankings files, in the order given. The label is
// hybrid(<a>+<b>); latency is the sum of the inputs' (the fusion itself is negligible).
public sealed class HybridRanker : IRanker
{
    private const int RrfK = 60;

    public string Label => "hybrid";

    public Rankings Rank(IReadOnlyList<CorpusFile> corpus, IReadOnlyList<EvalQuery> queries, RankerOptions options)
    {
        if (options.Inputs.Count < 2)
        {
            throw new ArgumentException("--ranker hybrid needs --inputs a.json,b.json (two or more rankings files)");
        }
        var inputs = options.Inputs.Select(Rankings.Read).ToList();
        var rankings = new Rankings($"hybrid({string.Join('+', inputs.Select(i => i.Label))})", [], []);
        foreach (var query in queries)
        {
            var clock = Stopwatch.StartNew();
            var lists = inputs.Select(i => (IReadOnlyList<string>)(i.Queries.TryGetValue(query.Id, out var l) ? l.Select(r => r.Ref).ToList() : [])).ToList();
            rankings.Queries[query.Id] = Rrf.Fuse(lists, RrfK);
            rankings.LatencyMs[query.Id] = clock.Elapsed.TotalMilliseconds + inputs.Sum(i => i.LatencyMs.GetValueOrDefault(query.Id));
        }
        return rankings;
    }
}
