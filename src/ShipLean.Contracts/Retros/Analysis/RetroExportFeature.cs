using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

public sealed record RetroExportFeature(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("goal")] string? Goal);
