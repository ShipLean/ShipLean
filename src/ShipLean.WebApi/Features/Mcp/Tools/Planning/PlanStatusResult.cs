using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// A plan's feature (its key) and its waves in order. StartableWave is the first wave not done, null when every one is.
public sealed record PlanStatusResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("feature")] string Feature,
    [property: JsonPropertyName("waves")] IReadOnlyList<PlanStatusWave> Waves,
    [property: JsonPropertyName("startable_wave")] string? StartableWave) : McpResult(Project, OpenUrl);
