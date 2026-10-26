using System.Text.Json;

namespace SearchEmbeddings;

// CA2: results/<label>.json. Scores are null for unscored rankers.
public sealed record Rankings(string Label, Dictionary<string, List<RankedRef>> Queries, Dictionary<string, double> LatencyMs)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static Rankings Read(string path)
        => JsonSerializer.Deserialize<Rankings>(File.ReadAllText(path), Json) ?? throw new InvalidDataException(path);

    public void Write(string path)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } dir)
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }
}
