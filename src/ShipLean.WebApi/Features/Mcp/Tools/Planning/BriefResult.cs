using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// Kind is task, story or feature. A task's markdown is the brief its agent works from, a story's the lead's brief, a
// feature's the conductor view (its waves, each a table of stories). Waves and StartableWave are set for a feature only.
public sealed record BriefResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("markdown")] string Markdown,
    [property: JsonPropertyName("waves")] IReadOnlyList<BriefWave>? Waves,
    [property: JsonPropertyName("startable_wave")] string? StartableWave) : McpResult(Project, OpenUrl);
