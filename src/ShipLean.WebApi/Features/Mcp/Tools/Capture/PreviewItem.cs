using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

public sealed record PreviewItem(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("parent")] string? Parent,
    [property: JsonPropertyName("sprint")] string? Sprint,
    [property: JsonPropertyName("points")] decimal? Points);
