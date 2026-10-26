using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

internal sealed record FlagCounts(
    [property: JsonPropertyName("unestimated")] int Unestimated,
    [property: JsonPropertyName("no_acceptance_criteria")] int NoAcceptanceCriteria,
    [property: JsonPropertyName("no_parent")] int NoParent,
    [property: JsonPropertyName("stale")] int Stale,
    [property: JsonPropertyName("likely_duplicate")] int LikelyDuplicate);
