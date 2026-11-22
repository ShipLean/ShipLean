using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// A pattern of an earlier analysis, with the centroid its embedding model gave it, so a later run with the same model can fit new points into it.
public sealed record RetroExportPattern(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("kind")] RetroPatternKind Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("lesson")] string Lesson,
    [property: JsonPropertyName("embedding_model")] string EmbeddingModel,
    [property: JsonPropertyName("centroid")] IReadOnlyList<float> Centroid);
