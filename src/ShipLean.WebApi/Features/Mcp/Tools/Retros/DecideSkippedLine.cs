using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record DecideSkippedLine(
    [property: JsonPropertyName("point")] Guid Point,
    [property: JsonPropertyName("reason")] string Reason);
