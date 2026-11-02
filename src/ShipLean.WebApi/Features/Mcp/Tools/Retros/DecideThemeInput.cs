using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record DecideThemeInput
{
    [JsonPropertyName("point"), Description("The id of the retro point.")]
    public required Guid Point { get; init; }

    [JsonPropertyName("theme"), Description("The theme name to give it. A person's choice is never overwritten.")]
    public required string Theme { get; init; }
}
