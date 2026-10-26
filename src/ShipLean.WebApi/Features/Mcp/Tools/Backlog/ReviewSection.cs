using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

internal sealed record ReviewSection(
    [property: JsonPropertyName("heading")] string Heading,
    [property: JsonPropertyName("text")] string Text);
