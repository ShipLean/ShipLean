using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// applied false: a preview, nothing moved. Items are the unfinished stories and bugs, each with where it goes.
public sealed record EndSprintResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("items")] IReadOnlyList<SprintItemLine> Items,
    [property: JsonPropertyName("items_points")] decimal ItemsPoints,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("plan")] string? Plan,
    [property: JsonPropertyName("expires_in_minutes")] int? ExpiresInMinutes) : McpResult(Project, OpenUrl);
