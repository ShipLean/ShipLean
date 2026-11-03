using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// Decision is undecided, actioned or notNeeded, and null on a went well point. Category is wentWell, wentBadly, improve or notInPlan; Role is conductor, subAgent or person. Item and Sprint are a work item key and a sprint name.
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
    [property: JsonPropertyName("agent_name")] string? AgentName,
    [property: JsonPropertyName("decision")] string? Decision,
    [property: JsonPropertyName("action")] RetroActionLine? Action,
    [property: JsonPropertyName("not_needed_reason")] string? NotNeededReason);
