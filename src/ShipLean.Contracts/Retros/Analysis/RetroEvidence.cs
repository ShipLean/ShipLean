using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// Over one pattern's points only: how many, and which items, Features, sprints and agents they span, and the first and last day they were logged.
public sealed record RetroEvidence(
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("items")] IReadOnlyList<string> Items,
    [property: JsonPropertyName("features")] IReadOnlyList<string> Features,
    [property: JsonPropertyName("sprints")] IReadOnlyList<string> Sprints,
    [property: JsonPropertyName("agents")] IReadOnlyList<string> Agents,
    [property: JsonPropertyName("first_logged")] DateTimeOffset? FirstLogged,
    [property: JsonPropertyName("last_logged")] DateTimeOffset? LastLogged);
