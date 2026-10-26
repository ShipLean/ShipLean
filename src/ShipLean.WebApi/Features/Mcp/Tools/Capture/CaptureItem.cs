using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

// One item an agent asks capture_work to create. Everything but type and title is optional.
public sealed record CaptureItem
{
    [JsonPropertyName("type"), Description("epic, feature, story, bug or task.")]
    public string? Type { get; init; }

    [JsonPropertyName("title"), Description("A short title in the team's words.")]
    public string? Title { get; init; }

    [JsonPropertyName("description"), Description("What and why, in Markdown.")]
    public string? Description { get; init; }

    [JsonPropertyName("acceptance_criteria"), Description("The acceptance criteria of a story, one checklist line each.")]
    public IReadOnlyList<string>? AcceptanceCriteria { get; init; }

    [JsonPropertyName("repro_steps"), Description("How to reproduce a bug, in Markdown.")]
    public string? ReproSteps { get; init; }

    [JsonPropertyName("severity"), Description("A bug's severity, 1 (critical) to 4 (low).")]
    public int? Severity { get; init; }

    [JsonPropertyName("points"), Description("Story points.")]
    public decimal? Points { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    [JsonPropertyName("priority"), Description("1 (highest) to 4. Leave out for the default.")]
    public int? Priority { get; init; }

    [JsonPropertyName("parent"), Description("The key of the parent item, such as SHIP-12.")]
    public string? Parent { get; init; }

    [JsonPropertyName("sprint"), Description("current, next, a sprint's name, or none.")]
    public string? Sprint { get; init; }
}
