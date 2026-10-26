using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// What one retro said to change next time: the feature it was on, the retro's file name, and the text.
public sealed record PlanLesson(
    [property: JsonPropertyName("feature")] string Feature,
    [property: JsonPropertyName("retro")] string Retro,
    [property: JsonPropertyName("text")] string Text);
