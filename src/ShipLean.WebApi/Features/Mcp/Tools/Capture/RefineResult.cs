using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

public sealed record RefineResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("revision")] int Revision,
    [property: JsonPropertyName("changed")] IReadOnlyList<string> Changed,
    [property: JsonPropertyName("notices")] IReadOnlyList<string> Notices,
    [property: JsonPropertyName("note_added")] bool? NoteAdded,
    [property: JsonPropertyName("note_error")] string? NoteError) : McpResult(Project, OpenUrl);
