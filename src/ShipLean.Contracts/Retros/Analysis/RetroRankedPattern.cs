using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

public sealed record RetroRankedPattern(
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("kind")] RetroPatternKind Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("continues")] Guid? Continues,
    [property: JsonPropertyName("evidence")] RetroEvidence Evidence);
