using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// A story a capture_plan preview would create, with how many tasks it holds. Lane is null for a wave's only lane.
public sealed record PlanPreviewStory(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("wave")] string Wave,
    [property: JsonPropertyName("lane")] string? Lane,
    [property: JsonPropertyName("tasks")] int Tasks);
