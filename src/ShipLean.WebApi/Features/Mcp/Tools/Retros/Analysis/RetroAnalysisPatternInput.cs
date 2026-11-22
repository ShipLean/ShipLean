using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

public sealed record RetroAnalysisPatternInput
{
    [JsonPropertyName("ref"), Description("Your own label for the pattern, unique in this run. Fixes name it in pattern_ref.")]
    public required string Ref { get; init; }

    [JsonPropertyName("kind"), Description("problem or wentWell.")]
    public required string Kind { get; init; }

    [JsonPropertyName("title"), Description("The pattern's title.")]
    public required string Title { get; init; }

    [JsonPropertyName("lesson"), Description("The generic lesson the pattern teaches.")]
    public required string Lesson { get; init; }

    [JsonPropertyName("asks"), Description("What the pattern asks of the team, one line each.")]
    public IReadOnlyList<string>? Asks { get; init; }

    [JsonPropertyName("point_ids"), Description("The ids of the points in the pattern. Each has outcome clustered and sits in no other pattern.")]
    public required IReadOnlyList<Guid> PointIds { get; init; }

    [JsonPropertyName("centroid"), Description("The pattern's centroid under embedding_model, so a later run can fit new points into it.")]
    public IReadOnlyList<float>? Centroid { get; init; }

    [JsonPropertyName("continues"), Description("An earlier pattern's id (same project, same embedding_model) that this run's points join.")]
    public Guid? Continues { get; init; }
}
