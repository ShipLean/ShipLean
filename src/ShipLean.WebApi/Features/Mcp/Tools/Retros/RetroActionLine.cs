using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// The action item a point was actioned into: its key and its state's name.
public sealed record RetroActionLine(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("state")] string State);
