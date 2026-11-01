using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record RetroPointInput
{
    [JsonPropertyName("text"), Description("The point, in plain words.")]
    public string? Text { get; init; }

    [JsonPropertyName("theme"), Description("The theme this point belongs under. Reuse a theme name from the project's list when one fits; a new name creates a theme. Leave out to let ShipLean choose, or to leave it unthemed.")]
    public string? Theme { get; init; }

    [JsonPropertyName("root_cause"), Description("Only went_badly points take a root cause; on any other category the call is refused.")]
    public string? RootCause { get; init; }
}
