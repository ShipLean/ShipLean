using System.Text.Json;

namespace SearchEmbeddings.Corpus;

public static class CorpusLoader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // A file, or every *.json in a directory.
    public static IReadOnlyList<CorpusFile> LoadCorpus(string path)
        => [.. Files(path).Select(f => JsonSerializer.Deserialize<CorpusFile>(File.ReadAllText(f), Json) ?? throw new InvalidDataException(f))];

    public static IReadOnlyList<EvalQuery> LoadQueries(string path)
        => [.. Files(path).SelectMany(f => JsonSerializer.Deserialize<List<EvalQuery>>(File.ReadAllText(f), Json) ?? throw new InvalidDataException(f))];

    private static IEnumerable<string> Files(string path)
        => Directory.Exists(path) ? Directory.GetFiles(path, "*.json").Order(StringComparer.Ordinal) : [path];
}
