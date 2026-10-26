using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// applied false: a preview, nothing moved (pass `plan` back to apply). Room is the points the sprint can take and
// RoomSource says where it came from (points_limit, capacity, velocity); both are null when none is known, and then
// NeedsPointsLimit is true, Proposed is empty and there is no plan. NextInStack is the top of what the cut line left out.
public sealed record PlanSprintResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("room")] decimal? Room,
    [property: JsonPropertyName("room_source")] string? RoomSource,
    [property: JsonPropertyName("already_in_sprint")] decimal AlreadyInSprint,
    [property: JsonPropertyName("items")] IReadOnlyList<SprintItemLine> Items,
    [property: JsonPropertyName("items_points")] decimal ItemsPoints,
    [property: JsonPropertyName("next_in_stack")] IReadOnlyList<SprintItemLine> NextInStack,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("needs_points_limit")] bool NeedsPointsLimit,
    [property: JsonPropertyName("plan")] string? Plan,
    [property: JsonPropertyName("expires_in_minutes")] int? ExpiresInMinutes) : McpResult(Project, OpenUrl);
