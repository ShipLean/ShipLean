using SearchEmbeddings;
using SearchEmbeddings.Baseline;
using SearchEmbeddings.Corpus;
using SearchEmbeddings.Embedding;

// CA3. Wave 1 slices append an entry here and nothing else in this file.
var rankers = new Dictionary<string, Func<IRanker>>(StringComparer.Ordinal)
{
    ["ilike"] = () => new IlikeRanker(),
    ["e5-int8-mean"] = () => new SemanticRanker("e5-int8-mean", ModelSpec.E5Int8, Pooling.Mean),
    ["e5-int8-cls"] = () => new SemanticRanker("e5-int8-cls", ModelSpec.E5Int8, Pooling.Cls),
    ["e5-fp32-mean"] = () => new SemanticRanker("e5-fp32-mean", ModelSpec.E5Fp32, Pooling.Mean),
    ["minilm-mean"] = () => new SemanticRanker("minilm-mean", ModelSpec.MiniLm, Pooling.Mean),
    ["tokenize"] = () => new TokenizeRanker(),
    ["lexical"] = () => new SearchEmbeddings.Lexical.LexicalRanker(),
    ["lexical-or"] = () => new SearchEmbeddings.Lexical.LexicalRanker(orBody: true),
    ["lexical-ortitle"] = () => new SearchEmbeddings.Lexical.LexicalRanker(orTitle: true),
    ["titles-and"] = () => new SearchEmbeddings.Lexical.LexicalRanker(titleOnly: true),
    ["titles-or"] = () => new SearchEmbeddings.Lexical.LexicalRanker(orTitle: true, titleOnly: true),
    ["minilm256-mean"] = () => new SemanticRanker("minilm256-mean", ModelSpec.MiniLm256, Pooling.Mean),
    ["keyfirst"] = () => new SearchEmbeddings.Hybrid.KeyFirstRanker(),
    ["hybrid"] = () => new SearchEmbeddings.Hybrid.HybridRanker(),
    ["sweep"] = () => new SearchEmbeddings.Hybrid.CutoffSweepRanker(),
};

const string Usage = "usage: --ranker <name> [--out file] [--chunk-size n] [--model-dir dir] [--inputs a.json,b.json] [--cutoffs from:to:step] [--corpus file|dir] [--queries file|dir]";
var flags = ParseFlags(args);
if (flags is null)
{
    return 2;
}
if (!flags.TryGetValue("ranker", out var name) || !rankers.TryGetValue(name, out var create))
{
    Console.Error.WriteLine($"--ranker is required, one of: {string.Join(", ", rankers.Keys)}");
    return 2;
}
if (!int.TryParse(flags.GetValueOrDefault("chunk-size") ?? "1200", out var chunkSize) || chunkSize <= 0)
{
    Console.Error.WriteLine($"--chunk-size must be a positive integer, got '{flags["chunk-size"]}'");
    return 2;
}

var root = FindRoot();
var corpusPath = flags.GetValueOrDefault("corpus") ?? Path.Combine(root, "scripts/search-eval/sample/corpus.json");
var queriesPath = flags.GetValueOrDefault("queries") ?? DefaultQueries(corpusPath);
var options = new RankerOptions(
    ChunkSize: chunkSize,
    ModelDir: flags.GetValueOrDefault("model-dir"),
    Inputs: flags.TryGetValue("inputs", out var inputs) ? inputs.Split(',', StringSplitOptions.RemoveEmptyEntries) : [],
    Cutoffs: flags.GetValueOrDefault("cutoffs"));

var ranker = create();
var rankings = ranker.Rank(CorpusLoader.LoadCorpus(corpusPath), CorpusLoader.LoadQueries(queriesPath), options);
var output = flags.GetValueOrDefault("out") ?? Path.Combine(root, "scripts/search-eval/results", $"{ranker.Label}.json");
rankings.Write(output);
Console.WriteLine($"{ranker.Label}: {rankings.Queries.Count} queries -> {output}");
return 0;

// --flag value pairs; null (after printing why) for an unknown flag, a stray token or a flag without a value.
static Dictionary<string, string>? ParseFlags(string[] args)
{
    string[] known = ["ranker", "out", "chunk-size", "model-dir", "inputs", "cutoffs", "corpus", "queries"];
    var flags = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i += 2)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal) || !known.Contains(args[i][2..]))
        {
            Console.Error.WriteLine($"unknown argument '{args[i]}'\n{Usage}");
            return null;
        }
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine($"{args[i]} needs a value\n{Usage}");
            return null;
        }
        flags[args[i][2..]] = args[i + 1];
    }
    return flags;
}

// corpus/<name>.json pairs with queries/<name>.json, a corpus directory with its sibling queries directory, and
// <dir>/corpus.json with <dir>/queries.json.
static string DefaultQueries(string corpusPath)
{
    var full = Path.GetFullPath(corpusPath);
    if (Directory.Exists(full))
    {
        return Path.Combine(Path.GetDirectoryName(full.TrimEnd(Path.DirectorySeparatorChar))!, "queries");
    }
    var dir = Path.GetDirectoryName(full)!;
    return Path.GetFileName(dir) == "corpus" ? Path.Combine(Path.GetDirectoryName(dir)!, "queries", Path.GetFileName(full)) : Path.Combine(dir, "queries.json");
}

static string FindRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "scripts/search-eval")))
        {
            return dir.FullName;
        }
    }
    return Directory.GetCurrentDirectory();
}
