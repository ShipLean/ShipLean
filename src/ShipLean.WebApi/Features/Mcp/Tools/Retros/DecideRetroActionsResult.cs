using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// applied false: a preview, nothing written (pass `plan` back to apply). Created lists the keys of the action items an apply made.
internal sealed record DecideRetroActionsResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("would_create")] IReadOnlyList<DecideWouldCreateLine> WouldCreate,
    [property: JsonPropertyName("would_add")] IReadOnlyList<DecideWouldAddLine> WouldAdd,
    [property: JsonPropertyName("would_mark_not_needed")] IReadOnlyList<DecideWouldNotNeededLine> WouldMarkNotNeeded,
    [property: JsonPropertyName("would_theme")] int WouldTheme,
    [property: JsonPropertyName("would_propose_merges")] int WouldProposeMerges,
    [property: JsonPropertyName("skipped")] IReadOnlyList<DecideSkippedLine> Skipped,
    [property: JsonPropertyName("left_undecided")] int LeftUndecided,
    [property: JsonPropertyName("created")] IReadOnlyList<string> Created,
    [property: JsonPropertyName("plan")] string? Plan,
    [property: JsonPropertyName("expires_in_minutes")] int? ExpiresInMinutes) : McpResult(Project, OpenUrl);
