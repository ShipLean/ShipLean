using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

// Moved lists the items in stack order; Sprint is null when they went back to the backlog.
internal sealed record MoveToSprintResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("sprint")] string? Sprint,
    [property: JsonPropertyName("moved")] IReadOnlyList<MovedItem> Moved) : McpResult(Project, OpenUrl);
