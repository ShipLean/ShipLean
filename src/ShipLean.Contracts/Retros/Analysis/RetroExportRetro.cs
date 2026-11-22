using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// What a retro adds to the points on a page, once per retro: Item is the key or null for a sprint retro.
public sealed record RetroExportRetro(
    [property: JsonPropertyName("item")] string? Item,
    [property: JsonPropertyName("sprint")] string? Sprint,
    [property: JsonPropertyName("agent_name")] string? AgentName,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("failure_log")] RetroExportFailureLog FailureLog);
