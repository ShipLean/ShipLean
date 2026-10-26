using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// The Feature a plan lands under: an existing one by key, or a new one from its title, goal and description.
public sealed record CapturePlanFeature
{
    [JsonPropertyName("key"), Description("An existing feature's key, such as SHIP-12. Leave out to create a new feature.")]
    public string? Key { get; init; }

    [JsonPropertyName("title"), Description("A new feature's title.")]
    public string? Title { get; init; }

    [JsonPropertyName("goal"), Description("What the feature is for, in a sentence or two.")]
    public string? Goal { get; init; }

    [JsonPropertyName("description"), Description("The feature's description in Markdown, with its ## Contracts section. Progress and reports belong in ShipLean (comments, report_task), not paths on the person's machine.")]
    public string? Description { get; init; }
}
