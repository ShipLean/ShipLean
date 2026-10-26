using System.Text.Json.Nodes;
using ShipLean.Application.Features.Mcp.Planning.Tags;

namespace ShipLean.TestSupport.Plans;

// A whole plan as capture_plan arguments (DtoFixtures/planning/*.json, without project and plan), plus a typed view of it
// for seeding and asserting. FeatureEditor is #40's shape: 6 stories (waves 0.1, 0.2, lanes F, A and L of wave 1, wave 2)
// and 23 tasks, lane F holding F1-F5; 30 items in all. It passes every plan shape rule with no warnings.
public sealed class PlanFixture(
    JsonObject arguments, PlanFixtureFeature feature, string sharedBranch, string commonBrief, string? planMarkdown,
    IReadOnlyList<PlanFixtureStory> stories)
{
    private static readonly Lazy<PlanFixture> _featureEditor = new(() => PlanFixtureLoader.Load("feature-editor-plan.json"));

    public static PlanFixture FeatureEditor => _featureEditor.Value;

    public PlanFixtureFeature Feature { get; } = feature;

    public string SharedBranch { get; } = sharedBranch;

    // The tags capture_plan writes on the Feature: branch: from the shared branch.
    public PlanTaskTags FeatureTags => new(Branch: SharedBranch);

    public string CommonBrief { get; } = commonBrief;

    public string? PlanMarkdown { get; } = planMarkdown;

    public IReadOnlyList<PlanFixtureStory> Stories { get; } = stories;

    // The Feature, its stories and their tasks.
    public int ItemCount => 1 + Stories.Count + Stories.Sum(story => story.Tasks.Count);

    // A fresh copy of the capture_plan arguments each call: change it, send it with McpTestClient, or deserialize it.
    public JsonObject Arguments() => (JsonObject)arguments.DeepClone();
}
