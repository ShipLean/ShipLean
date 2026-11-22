using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// The scope as an agent reads it back: Feature is a key, Sprint a name, From and To UTC days (both ends included).
public sealed record RetroScopeView(
    [property: JsonPropertyName("kind")] RetroScopeKind Kind,
    [property: JsonPropertyName("feature")] string? Feature,
    [property: JsonPropertyName("sprint")] string? Sprint,
    [property: JsonPropertyName("from")] DateOnly? From,
    [property: JsonPropertyName("to")] DateOnly? To);
