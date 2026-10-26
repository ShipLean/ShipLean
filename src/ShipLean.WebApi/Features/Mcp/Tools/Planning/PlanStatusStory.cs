using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

public sealed record PlanStatusStory(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("lane")] string? Lane,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("assignee")] string? Assignee,
    [property: JsonPropertyName("tasks")] IReadOnlyList<PlanStatusTask> Tasks);
