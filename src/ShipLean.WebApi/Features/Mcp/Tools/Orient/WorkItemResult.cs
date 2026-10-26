using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Tools.Feature;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

// Goal, Status, Verified, Slug and Started are a Feature's and are left out of the JSON for any other type (Started is the day, as the app labels it).
// parents are nearest first. comments and history are newest first: the last 5 comments and the last 10 revisions.
// comments_older is the `at` of the oldest comment in this page when older comments exist; pass it as comments_before for the next page.
public sealed record WorkItemResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("assignee")] string? Assignee,
    [property: JsonPropertyName("sprint")] string? Sprint,
    [property: JsonPropertyName("priority")] int Priority,
    [property: JsonPropertyName("points")] decimal? Points,
    [property: JsonPropertyName("severity")] int? Severity,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("acceptance_criteria")] IReadOnlyList<WorkItemResult.Criterion> AcceptanceCriteria,
    [property: JsonPropertyName("repro_steps")] string ReproSteps,
    [property: JsonPropertyName("revision")] int Revision,
    [property: JsonPropertyName("parents")] IReadOnlyList<WorkItemResult.Ref> Parents,
    [property: JsonPropertyName("children")] IReadOnlyList<WorkItemResult.Child> Children,
    [property: JsonPropertyName("links")] IReadOnlyList<WorkItemResult.Link> Links,
    [property: JsonPropertyName("comments")] IReadOnlyList<WorkItemResult.Comment> Comments,
    [property: JsonPropertyName("comments_older")] string? CommentsOlder,
    [property: JsonPropertyName("attachments")] IReadOnlyList<WorkItemResult.Attachment> Attachments,
    [property: JsonPropertyName("history")] IReadOnlyList<WorkItemResult.HistoryEntry> History,
    [property: JsonPropertyName("goal")] string? Goal = null,
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("verified")] VerifiedLine? Verified = null,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("started")] string? Started = null) : McpResult(Project, OpenUrl)
{
    public sealed record Criterion(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("done")] bool Done);

    public sealed record Ref(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title);

    public sealed record Child(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("assignee")] string? Assignee);

    public sealed record Link(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("key")] string? Key,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("url")] string? Url);

    public sealed record Comment(
        [property: JsonPropertyName("author")] string? Author,
        [property: JsonPropertyName("agent_name")] string? AgentName,
        [property: JsonPropertyName("at")] string At,
        [property: JsonPropertyName("body")] string Body);

    public sealed record Attachment(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size_bytes")] long SizeBytes,
        [property: JsonPropertyName("content_type")] string ContentType,
        [property: JsonPropertyName("is_markdown")] bool IsMarkdown,
        [property: JsonPropertyName("uploaded_by")] string? UploadedBy,
        [property: JsonPropertyName("at")] string At,
        [property: JsonPropertyName("agent_name")] string? AgentName,
        [property: JsonPropertyName("via")] string? Via);

    public sealed record HistoryEntry(
        [property: JsonPropertyName("revision")] int Number,
        [property: JsonPropertyName("who")] string? Who,
        [property: JsonPropertyName("agent_name")] string? AgentName,
        [property: JsonPropertyName("at")] string At,
        [property: JsonPropertyName("changes")] IReadOnlyList<Change> Changes);

    public sealed record Change(
        [property: JsonPropertyName("field")] string Field,
        [property: JsonPropertyName("from")] string? From,
        [property: JsonPropertyName("to")] string? To);
}
