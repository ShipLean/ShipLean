using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// One task of a plan: the brief one agent works from, the model role it runs on and the context it should fit in.
public sealed record CapturePlanTask
{
    [JsonPropertyName("title"), Description("A short title.")]
    public string? Title { get; init; }

    [JsonPropertyName("brief"), Description("The task's own brief in Markdown: goal, what to read, what it owns, its tests.")]
    public string? Brief { get; init; }

    [JsonPropertyName("model"), Description("The model role the task runs on, as the planning guide names them.")]
    public string? Model { get; init; }

    [JsonPropertyName("context_k"), Description("The context the task should fit in, in thousands of tokens.")]
    public int? ContextK { get; init; }

    [JsonPropertyName("points"), Description("Story points, when the team estimates tasks.")]
    public decimal? Points { get; init; }
}
