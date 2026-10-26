using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

public sealed record BriefStory(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("lane")] string? Lane,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("done")] bool Done);
