using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

public sealed record RetroViewPattern(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("kind")] RetroPatternKind Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("lesson")] string Lesson,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("continues")] Guid? Continues,
    [property: JsonPropertyName("evidence")] RetroEvidence Evidence,
    [property: JsonPropertyName("fixes")] IReadOnlyList<RetroViewFix> Fixes);
