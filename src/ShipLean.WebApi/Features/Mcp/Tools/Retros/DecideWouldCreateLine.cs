using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record DecideWouldCreateLine(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("fix_type")] string FixType,
    [property: JsonPropertyName("theme")] string? Theme,
    [property: JsonPropertyName("covers")] int Covers,
    [property: JsonPropertyName("possible_duplicates")] IReadOnlyList<DecideDuplicateLine> PossibleDuplicates);
