using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// Done when every story in the wave is completed or removed.
public sealed record BriefWave(
    [property: JsonPropertyName("wave")] string Wave,
    [property: JsonPropertyName("done")] bool Done,
    [property: JsonPropertyName("stories")] IReadOnlyList<BriefStory> Stories);
