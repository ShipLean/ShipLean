using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// Brief is the item's description, or a Feature's goal, cut to 2,000 characters with an ellipsis.
public sealed record RetroExportItem(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("brief")] string? Brief);
