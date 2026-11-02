using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record DecideActionInput
{
    [JsonPropertyName("title"), Description("The action item's title.")]
    public string? Title { get; init; }

    [JsonPropertyName("fix_type"), Description("skill, guide, test, analyzer, product or other.")]
    public string? FixType { get; init; }

    [JsonPropertyName("points"), Description("The ids of the retro points this action covers.")]
    public IReadOnlyList<Guid>? Points { get; init; }

    [JsonPropertyName("theme"), Description("The theme name. Leave out for the commonest theme among the points.")]
    public string? Theme { get; init; }

    [JsonPropertyName("existing"), Description("The key of an open action item to add the points to instead of creating one.")]
    public string? Existing { get; init; }
}
