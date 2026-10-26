using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

// matches are best first: items, then sprints, then projects. semantic is true when the words were also matched by meaning. project is the one project searched (null across several).
// A filter-only call has no matched_in or snippet. attachment names the Markdown file whose text matched. more: the search found others beyond these.
public sealed record SearchResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("semantic")] bool Semantic,
    [property: JsonPropertyName("matches")] IReadOnlyList<SearchResult.Match> Matches,
    [property: JsonPropertyName("more")] bool More) : McpResult(Project, OpenUrl)
{
    // kind is item, sprint or project. An item has key..tags; a sprint has goal and dates; a project has its key and name. The title of a sprint hit is "name: goal".
    public sealed record Match(
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("project")] string ProjectKey,
        [property: JsonPropertyName("open_url")] string OpenUrl,
        [property: JsonPropertyName("key")] string? Key = null,
        [property: JsonPropertyName("type")] string? Type = null,
        [property: JsonPropertyName("state")] string? State = null,
        [property: JsonPropertyName("parent")] string? Parent = null,
        [property: JsonPropertyName("assignee")] string? Assignee = null,
        [property: JsonPropertyName("sprint")] string? Sprint = null,
        [property: JsonPropertyName("points")] decimal? Points = null,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags = null,
        [property: JsonPropertyName("goal")] string? Goal = null,
        [property: JsonPropertyName("dates")] string? Dates = null,
        [property: JsonPropertyName("matched_in")] string? MatchedIn = null,
        [property: JsonPropertyName("snippet")] string? Snippet = null,
        [property: JsonPropertyName("attachment")] string? Attachment = null);
}
