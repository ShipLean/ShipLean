using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// The task the report landed on and the state it is in now, the report comment, how many commit and PR links were added,
// and the tasks of the same story left marked orphan.
public sealed record ReportResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("comment_id")] Guid CommentId,
    [property: JsonPropertyName("links_added")] int LinksAdded,
    [property: JsonPropertyName("orphans")] IReadOnlyList<OrphanTask> Orphans) : McpResult(Project, OpenUrl);
