using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

public sealed record BreakDownChild
{
    [JsonPropertyName("title"), Description("The child's title.")]
    public string? Title { get; init; }

    [JsonPropertyName("points"), Description("Story points, if known.")]
    public decimal? Points { get; init; }

    [JsonPropertyName("description"), Description("The child's description, as Markdown.")]
    public string? Description { get; init; }

    [JsonPropertyName("tags"), Description("Tags to put on the child.")]
    public IReadOnlyList<string>? Tags { get; init; }
}
