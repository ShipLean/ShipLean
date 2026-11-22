using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

public sealed record RetroViewAction(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("state")] string State);
