using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record DecideWouldNotNeededLine(
    [property: JsonPropertyName("point")] Guid Point,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("reason")] string Reason);
