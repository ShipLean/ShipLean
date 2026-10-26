using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

public sealed record CapturedStory(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("wave")] string Wave,
    [property: JsonPropertyName("lane")] string? Lane,
    [property: JsonPropertyName("tasks")] IReadOnlyList<CapturedTask> Tasks);
