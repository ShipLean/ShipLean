using System.Globalization;
using SearchEmbeddings.Corpus;

namespace SearchEmbeddings.Hybrid;

// --ranker sweep --inputs scored.json --cutoffs 0.45:0.95:0.05: for each cut-off keeps the hits scoring >= it and prints one row
// per cut-off with top-1, top-5, MRR (same definitions as metrics.py) and the mean number of hits returned, per query kind
// group. Rank is the file's order, as the live ranker's. Returns the input unchanged (a sweep makes no single ranking).
public sealed class CutoffSweepRanker : IRanker
{
    public string Label => "sweep";

    public Rankings Rank(IReadOnlyList<CorpusFile> corpus, IReadOnlyList<EvalQuery> queries, RankerOptions options)
    {
        if (options.Inputs.Count != 1 || options.Cutoffs is null)
        {
            throw new ArgumentException("--ranker sweep needs --inputs scored.json and --cutoffs from:to:step");
        }
        var parts = options.Cutoffs.Split(':');
        if (parts.Length != 3 || !double.TryParse(parts[0], CultureInfo.InvariantCulture, out var from)
            || !double.TryParse(parts[1], CultureInfo.InvariantCulture, out var to)
            || !double.TryParse(parts[2], CultureInfo.InvariantCulture, out var step) || step <= 0 || to < from)
        {
            throw new ArgumentException($"--cutoffs must be from:to:step with step > 0, got '{options.Cutoffs}'");
        }
        var input = Rankings.Read(options.Inputs[0]);
        if (input.Queries.Values.Any(l => l.Any(r => r.Score is null)))
        {
            throw new ArgumentException($"{options.Inputs[0]} is not a scored rankings file (null scores)");
        }

        var groups = new (string Name, Func<EvalQuery, bool> Match)[]
        {
            ("all", _ => true),
            ("paraphrase+bodyOnly", q => q.Kind is "paraphrase" or "bodyOnly"),
            ("exactKey+titleWord", q => q.Kind is "exactKey" or "titleWord"),
        };
        Console.WriteLine($"sweep of {input.Label} ({queries.Count} queries); hits = mean results per query");
        Console.WriteLine("| cut-off | group | n | top-1 | top-5 | MRR | hits |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (var c = from; c <= to + 1e-9; c += step)
        {
            foreach (var (name, match) in groups)
            {
                var rows = queries.Where(match).Select(q => Score(q, input.Queries.GetValueOrDefault(q.Id)?.Where(r => r.Score >= c - 1e-9).Select(r => r.Ref).ToList() ?? [])).ToList();
                if (rows.Count == 0)
                {
                    continue;
                }
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"| {c:0.00} | {name} | {rows.Count} | {rows.Average(r => r.Top1):0.00} | {rows.Average(r => r.Top5):0.00} | {rows.Average(r => r.Rr):0.00} | {rows.Average(r => r.Hits):0.0} |"));
            }
        }
        return input;
    }

    private static (double Top1, double Top5, double Rr, int Hits) Score(EvalQuery query, List<string> ranked)
    {
        var wanted = query.Expected.ToHashSet(StringComparer.Ordinal);
        var first = ranked.FindIndex(wanted.Contains);
        return (ranked.Count > 0 && wanted.Contains(ranked[0]) ? 1 : 0, ranked.Take(5).Any(wanted.Contains) ? 1 : 0, first < 0 ? 0 : 1.0 / (first + 1), ranked.Count);
    }
}
