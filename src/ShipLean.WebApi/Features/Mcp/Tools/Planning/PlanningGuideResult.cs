using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// One part of the planning guide as Markdown, the names of every part, and the lessons the project's past retros wrote down.
public sealed record PlanningGuideResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("part")] string Part,
    [property: JsonPropertyName("markdown")] string Markdown,
    [property: JsonPropertyName("parts")] IReadOnlyList<string> Parts,
    [property: JsonPropertyName("lessons")] IReadOnlyList<PlanLesson> Lessons) : McpResult(Project, OpenUrl);
