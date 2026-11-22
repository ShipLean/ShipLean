using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

public sealed record RetroAnalysisPointInput
{
    [JsonPropertyName("id"), Description("The point's id, from export_retro_points.")]
    public required Guid Id { get; init; }

    [JsonPropertyName("outcome"), Description("noise, folded, oneOff, clustered or unclustered.")]
    public required string Outcome { get; init; }

    [JsonPropertyName("folded_into"), Description("For folded: the id of the point this one repeats.")]
    public Guid? FoldedInto { get; init; }

    [JsonPropertyName("generic"), Description("The generic rewrite of the point. Set for clustered, unclustered and oneOff.")]
    public string? Generic { get; init; }
}
