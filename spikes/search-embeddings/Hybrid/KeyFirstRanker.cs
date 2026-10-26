using System.Text.RegularExpressions;
using SearchEmbeddings.Corpus;

namespace SearchEmbeddings.Hybrid;

// --ranker keyfirst --inputs ranked.json: models production's exact-key path (B3d). When the whole query matches
// ^[A-Za-z]{2,4}-?\d{1,9}$ and the normalised key (PROJECTKEY-n) names an item in the corpus, that item goes first; the rest of the
// input follows. Other queries pass through. Label is keyfirst(<input>).
public sealed partial class KeyFirstRanker : IRanker
{
    public string Label => "keyfirst";

    public Rankings Rank(IReadOnlyList<CorpusFile> corpus, IReadOnlyList<EvalQuery> queries, RankerOptions options)
    {
        if (options.Inputs.Count != 1)
        {
            throw new ArgumentException("--ranker keyfirst needs --inputs ranked.json");
        }
        var input = Rankings.Read(options.Inputs[0]);

        // Item refs are "<project ref>-<nnn>" in file order, so the number is the item's position in its project: PROJECTKEY-n.
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in corpus)
        {
            foreach (var item in file.Items.Where(i => i.State != "Removed"))
            {
                var number = item.Ref[(item.Ref.LastIndexOf('-') + 1)..].TrimStart('0');
                byKey[$"{file.Project.Key.ToUpperInvariant()}-{number}"] = item.Ref;
            }
        }

        var rankings = new Rankings($"keyfirst({input.Label})", [], []);
        foreach (var query in queries)
        {
            var list = input.Queries.GetValueOrDefault(query.Id) ?? [];
            var match = KeyShape().Match(query.Text.Trim());
            if (match.Success && byKey.TryGetValue($"{match.Groups[1].Value.ToUpperInvariant()}-{match.Groups[2].Value.TrimStart('0')}", out var hit))
            {
                list = [new RankedRef(hit, null), .. list.Where(r => r.Ref != hit)];
            }
            rankings.Queries[query.Id] = list;
            rankings.LatencyMs[query.Id] = input.LatencyMs.GetValueOrDefault(query.Id);
        }
        return rankings;
    }

    [GeneratedRegex(@"^([A-Za-z]{2,4})-?(\d{1,9})$")]
    private static partial Regex KeyShape();
}
