using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

// NoteAdded is null when no note was asked for; false means the hand-back stands but the note did not land.
internal sealed record HandBackResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("revision")] int Revision,
    [property: JsonPropertyName("note_added")] bool? NoteAdded,
    [property: JsonPropertyName("note_error")] string? NoteError) : McpResult(Project, OpenUrl);
