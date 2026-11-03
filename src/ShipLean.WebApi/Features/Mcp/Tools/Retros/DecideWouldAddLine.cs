using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record DecideWouldAddLine(
    [property: JsonPropertyName("existing")] string Existing,
    [property: JsonPropertyName("covers")] int Covers);
