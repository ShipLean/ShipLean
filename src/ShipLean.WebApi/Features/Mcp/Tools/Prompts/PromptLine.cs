using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// One prompt in the prompts list: its key, where it runs, whether a custom version is in use, and how many versions it has.
public sealed record PromptLine(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("stage")] string Stage,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("in_use_version")] int? InUseVersion,
    [property: JsonPropertyName("versions")] int Versions,
    [property: JsonPropertyName("placeholders")] IReadOnlyList<string> Placeholders);
