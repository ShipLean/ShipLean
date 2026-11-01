using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

public sealed record RetroFailureLogInput
{
    [JsonPropertyName("blockers"), Description("What specific constraints, missing data or tool failures stopped you.")]
    public IReadOnlyList<string>? Blockers { get; init; }

    [JsonPropertyName("assumptions_disproved"), Description("Strategies you tried that turned out to be wrong, and why.")]
    public IReadOnlyList<string>? AssumptionsDisproved { get; init; }

    [JsonPropertyName("friction"), Description("Steps that took longer or needed more retries than expected.")]
    public IReadOnlyList<string>? Friction { get; init; }
}
