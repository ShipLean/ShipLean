using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

// NoteAdded is null when no note was asked for; false means the change stands but the note did not land (NoteError says why).
// MoveParentTo is the state to offer for ParentKey when this closed the parent's last open task; the parent is not moved.
internal sealed record UpdateProgressResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("points")] decimal? Points,
    [property: JsonPropertyName("revision")] int Revision,
    [property: JsonPropertyName("link_added")] bool LinkAdded,
    [property: JsonPropertyName("note_added")] bool? NoteAdded,
    [property: JsonPropertyName("note_error")] string? NoteError,
    [property: JsonPropertyName("last_task_closed")] bool LastTaskClosed,
    [property: JsonPropertyName("parent_key")] string? ParentKey,
    [property: JsonPropertyName("move_parent_to")] string? MoveParentTo) : McpResult(Project, OpenUrl);
