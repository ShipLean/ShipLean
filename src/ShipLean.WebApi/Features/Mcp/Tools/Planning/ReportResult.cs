using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// The task the report landed on and the state it is in now, the report comment, how many commit and PR links were added,
// the tasks of the same story left marked orphan, how many code comments were answered, how many commits are now on the task, and the commits that stayed link cards.
public sealed record ReportResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("comment_id")] Guid CommentId,
    [property: JsonPropertyName("links_added")] int LinksAdded,
    [property: JsonPropertyName("orphans")] IReadOnlyList<OrphanTask> Orphans,
    [property: JsonPropertyName("answered_comments")] int AnsweredComments,
    [property: JsonPropertyName("commits_added")] int CommitsAdded,
    [property: JsonPropertyName("commits_not_added")] IReadOnlyList<CommitNotAdded> CommitsNotAdded) : McpResult(Project, OpenUrl);

// A commit that stayed a link card, and why: repo_unreadable, bad_sha or validation.
public sealed record CommitNotAdded(
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("repo")] string Repo,
    [property: JsonPropertyName("reason")] string Reason);
