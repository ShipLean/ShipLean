using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

public sealed record ListProjectsResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("projects")] IReadOnlyList<ListProjectsResult.ProjectLine> Projects) : McpResult(Project, OpenUrl)
{
    public sealed record ProjectLine(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("my_role")] string MyRole,
        [property: JsonPropertyName("current_sprint")] SprintLine? CurrentSprint);

    public sealed record SprintLine(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("start")] string Start,
        [property: JsonPropertyName("end")] string End);
}
