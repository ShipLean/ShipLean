using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// One story of a plan: a lane of one wave, and the tasks the agents in that lane work through.
public sealed record CapturePlanStory
{
    [JsonPropertyName("title"), Description("A short title for the lane's work.")]
    public string? Title { get; init; }

    [JsonPropertyName("wave"), Description("The wave it runs in, such as 0, 0.1, 1 or 2. Waves run in order; the stories of one wave run side by side.")]
    public string? Wave { get; init; }

    [JsonPropertyName("lane"), Description("The lane's name inside its wave. Leave out when the wave has one lane.")]
    public string? Lane { get; init; }

    [JsonPropertyName("description"), Description("What the lane delivers, in Markdown.")]
    public string? Description { get; init; }

    [JsonPropertyName("tasks"), Description("The lane's tasks, in the order they run.")]
    public IReadOnlyList<CapturePlanTask>? Tasks { get; init; }
}
