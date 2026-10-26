using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

internal sealed record ReviewedItem(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("points")] decimal? Points,
    [property: JsonPropertyName("days_since_change")] int DaysSinceChange,
    [property: JsonPropertyName("flags")] IReadOnlyList<string> Flags,
    [property: JsonPropertyName("duplicates")] IReadOnlyList<string> Duplicates);
