using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record RetroBadPointInput
{
    [JsonPropertyName("text"), Description("The point, in plain words.")]
    public string? Text { get; init; }

    [JsonPropertyName("theme"), Description("The theme this point belongs under. Reuse a theme name from the project's list when one fits; a new name creates a theme. Leave out to let ShipLean choose, or to leave it unthemed.")]
    public string? Theme { get; init; }

    [JsonPropertyName("root_cause"), Description("The root cause of the failure, stated as a fact about the system, not about a person.")]
    public string? RootCause { get; init; }
}
