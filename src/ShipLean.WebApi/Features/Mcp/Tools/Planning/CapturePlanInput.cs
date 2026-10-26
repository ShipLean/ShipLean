using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// A whole plan as capture_plan received it: what IPlanShapeRules checks.
public sealed record CapturePlanInput
{
    [JsonPropertyName("feature")]
    public CapturePlanFeature? Feature { get; init; }

    [JsonPropertyName("shared_branch")]
    public string? SharedBranch { get; init; }

    [JsonPropertyName("common_brief")]
    public string? CommonBrief { get; init; }

    [JsonPropertyName("plan_markdown")]
    public string? PlanMarkdown { get; init; }

    [JsonPropertyName("stories")]
    public IReadOnlyList<CapturePlanStory>? Stories { get; init; }
}
