using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// One proposal an agent made to fold a theme into another, waiting for a person's answer.
public sealed record ThemeMergeProposalLine(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("into")] string Into,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("proposed_by")] string ProposedBy,
    [property: JsonPropertyName("point_count")] int PointCount);

// applied false: a preview, nothing written (pass `plan` back to apply). Merge and KeepApart are the proposals the plan holds, or the ones just answered;
// Pending is what still waits for an answer afterwards.
internal sealed record DecideThemeMergesResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("merge")] IReadOnlyList<ThemeMergeProposalLine> Merge,
    [property: JsonPropertyName("keep_apart")] IReadOnlyList<ThemeMergeProposalLine> KeepApart,
    [property: JsonPropertyName("pending")] IReadOnlyList<ThemeMergeProposalLine> Pending,
    [property: JsonPropertyName("plan")] string? Plan,
    [property: JsonPropertyName("expires_in_minutes")] int? ExpiresInMinutes) : McpResult(Project, OpenUrl);
