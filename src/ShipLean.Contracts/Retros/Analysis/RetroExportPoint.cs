using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// Category is wentWell, wentBadly, improve or notInPlan; Role is conductor, subAgent or person. Item is null for a sprint retro; Feature is the item's Feature
// (the item itself on a Feature's retro), else null; Sprint is the retro's sprint, else the item's.
public sealed record RetroExportPoint(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("retro_id")] Guid RetroId,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("root_cause")] string? RootCause,
    [property: JsonPropertyName("theme")] string? Theme,
    [property: JsonPropertyName("logged_at")] DateTimeOffset LoggedAt,
    [property: JsonPropertyName("agent_name")] string? AgentName,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("item")] RetroExportItem? Item,
    [property: JsonPropertyName("feature")] RetroExportFeature? Feature,
    [property: JsonPropertyName("sprint")] string? Sprint,
    [property: JsonPropertyName("commits")] IReadOnlyList<RetroExportCommit> Commits);
