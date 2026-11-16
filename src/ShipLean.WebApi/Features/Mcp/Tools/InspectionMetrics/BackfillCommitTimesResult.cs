using System.ComponentModel;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.InspectionMetrics;

// What backfill_commit_times answers. Listing sets Missing and NextCursor; setting times sets Updated, AlreadySet and NotFound.
// TODO: temporary; remove once existing installs are backfilled
public sealed record BackfillCommitTimesResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("missing"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MissingCommitLink>? Missing,
    [property: JsonPropertyName("next_cursor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NextCursor,
    [property: JsonPropertyName("updated"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Updated,
    [property: JsonPropertyName("already_set"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AlreadySet,
    [property: JsonPropertyName("not_found"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NotFound) : McpResult(Project, OpenUrl);

public sealed record MissingCommitLink(
    [property: JsonPropertyName("sha7")] string Sha7,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("item")] string Item);

// One commit time an agent sends to backfill_commit_times.
public sealed record CommitTimeInput
{
    [JsonPropertyName("sha"), Description("The commit's sha: 7 to 64 hex characters. Links match on the first 7.")]
    public string? Sha { get; init; }

    [JsonPropertyName("committed_at"), Description("When the commit was authored, ISO 8601 with an offset, from git log --format='%H %aI'.")]
    public string? CommittedAt { get; init; }
}
