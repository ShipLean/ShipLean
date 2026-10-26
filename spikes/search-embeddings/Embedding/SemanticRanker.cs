using System.Diagnostics;
using SearchEmbeddings.Corpus;

namespace SearchEmbeddings.Embedding;

// Brute-force cosine over every chunk (the vectors are unit length, so a dot product). An item's score is its best chunk; the top Cap
// items are written with their scores so A3's --cutoffs sweep can run on the file. latencyMs is the query embedding plus the scan.
public sealed class SemanticRanker(string label, ModelSpec spec, Pooling pooling) : IRanker
{
    private const int Cap = 50;

    public string Label => label;

    public Rankings Rank(IReadOnlyList<CorpusFile> corpus, IReadOnlyList<EvalQuery> queries, RankerOptions options)
    {
        var dir = options.ModelDir ?? spec.DefaultDir;
        using var embedder = new OnnxEmbedder(dir, spec, pooling);
        var chunks = corpus.SelectMany(f => f.Items).Where(i => i.State != "Removed").SelectMany(i => Chunker.ChunksOf(i, options.ChunkSize)).ToList();
        var clock = Stopwatch.StartNew();
        var vectors = embedder.EmbedPassages([.. chunks.Select(c => c.Text)]);
        Console.Error.WriteLine($"{label}: embedded {chunks.Count} chunks in {clock.Elapsed.TotalSeconds:F1} s (chunk size {options.ChunkSize})");

        var rankings = new Rankings(label, [], []);
        foreach (var query in queries)
        {
            clock.Restart();
            var q = embedder.EmbedQuery(query.Text);
            var best = new Dictionary<string, double>(StringComparer.Ordinal);
            for (var i = 0; i < chunks.Count; i++)
            {
                var score = Dot(q, vectors[i]);
                if (!best.TryGetValue(chunks[i].ItemRef, out var seen) || score > seen)
                {
                    best[chunks[i].ItemRef] = score;
                }
            }
            rankings.Queries[query.Id] = [.. best.OrderByDescending(p => p.Value).Take(Cap).Select(p => new RankedRef(p.Key, p.Value))];
            rankings.LatencyMs[query.Id] = clock.Elapsed.TotalMilliseconds;
        }
        return rankings;
    }

    private static double Dot(float[] a, float[] b)
    {
        var sum = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }
        return sum;
    }
}
