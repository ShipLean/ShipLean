using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

// A code comment nobody has resolved, outdated and sent-back ones included. id is what report_task's answered_comments takes; side is old or new.
public sealed record OpenCodeComment(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("repo")] string Repo,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("line_text")] string LineText,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("by")] string By);
