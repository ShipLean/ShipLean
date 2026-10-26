using System.Text.Json;

namespace ShipLean.Application.Features.Search.Embedding;

// shiplean-model.json from the model directory (CB8).
public sealed record ModelManifest(string Id, string Revision, int Dimensions, int MaxTokens, string QueryPrefix, string PassagePrefix, string Pooling)
{
    public const string FileName = "shiplean-model.json";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static ModelManifest Read(string directory)
    {
        var path = Path.Combine(directory, FileName);
        var manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException($"{path} is empty.");
        if (manifest.Pooling != "mean")
        {
            throw new InvalidDataException($"{path}: pooling \"{manifest.Pooling}\" is not supported (only mean).");
        }
        // <s> and </s> take two of the tokens, and a vector needs a size.
        if (manifest.MaxTokens < 3 || manifest.Dimensions <= 0)
        {
            throw new InvalidDataException($"{path}: maxTokens must be at least 3 and dimensions above 0.");
        }
        return manifest;
    }
}
