using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// Category is wentWell, wentBadly, improve or notInPlan; Role is conductor, subAgent or person. Item and Sprint are a work item key and a sprint name.
public sealed record RetroPointLine(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("retro_id")] Guid RetroId,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("root_cause")] string? RootCause,
    [property: JsonPropertyName("theme")] string? Theme,
    [property: JsonPropertyName("item")] string? Item,
    [property: JsonPropertyName("sprint")] string? Sprint,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("agent_name")] string? AgentName);
