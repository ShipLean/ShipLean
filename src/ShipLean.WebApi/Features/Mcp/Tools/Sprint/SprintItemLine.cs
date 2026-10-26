using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// One story or bug in a plan_sprint or end_sprint answer. To (end_sprint only) is the sprint name or "backlog".
public sealed record SprintItemLine(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("points")] decimal? Points,
    [property: JsonPropertyName("to")] string? To = null);
