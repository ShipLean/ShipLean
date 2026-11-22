using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

public sealed record RetroAnalysisRow(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("scope")] RetroScopeView Scope,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("agent_name")] string? AgentName,
    [property: JsonPropertyName("patterns")] int Patterns,
    [property: JsonPropertyName("actions_new")] int ActionsNew);
