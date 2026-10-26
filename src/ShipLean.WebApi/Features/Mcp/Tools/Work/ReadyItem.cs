using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

internal sealed record ReadyItem(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("points")] decimal? Points,
    [property: JsonPropertyName("open_url")] string OpenUrl);
