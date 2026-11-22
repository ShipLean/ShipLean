using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

public sealed record RetroExportOpenAction(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("fix_type")] string? FixType,
    [property: JsonPropertyName("theme")] string? Theme,
    [property: JsonPropertyName("point_count")] int PointCount);
