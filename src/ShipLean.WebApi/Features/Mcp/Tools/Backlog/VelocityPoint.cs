using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

internal sealed record VelocityPoint(
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("committed")] decimal Committed,
    [property: JsonPropertyName("completed")] decimal Completed);
