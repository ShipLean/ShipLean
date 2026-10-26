using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// A plan's arguments as the tree to write. The shape rules have passed by now, so waves parse and titles are there.
// Tag placement is PlanTags': the Feature the branch:, a story its wave: and lane:, a task those plus its own model: and ctx:.
public static class CapturePlanMapper
{
    // A stable sort by wave: the stories of one wave keep the order they were sent in. A lane is trimmed (the rules compare
    // trimmed lanes), and a blank one is no lane.
    public static IReadOnlyList<PlannedStory> Sort(CapturePlanInput input)
        => [.. input.Stories!.Select(story => new PlannedStory(story with { Lane = story.Lane?.Trim() is { Length: > 0 } lane ? lane : null }, WaveLabel.Parse(story.Wave!))).OrderBy(planned => planned.Wave)];

    public static IReadOnlyList<WorkItemTreeCreate.Node> Stories(IReadOnlyList<PlannedStory> stories)
        => [.. stories.Select(planned => new WorkItemTreeCreate.Node(
            WorkItemTypeId.Story, planned.Story.Title!,
            Changes(planned.Story.Description, PlanTags.Format(new PlanTaskTags(Wave: planned.Wave, Lane: planned.Story.Lane))),
            [.. (planned.Story.Tasks ?? []).Select(task => Task(planned, task))]))];

    public static WorkItemTreeCreate.Node NewFeature(CapturePlanInput input, IReadOnlyList<PlannedStory> stories)
        => new(
            WorkItemTypeId.Feature, input.Feature!.Title!,
            Changes(input.Feature.Description, PlanTags.Format(new PlanTaskTags(Branch: input.SharedBranch))) with
            {
                Goal = input.Feature.Goal is { } goal ? new FieldSet<string>(goal) : null,
            },
            Stories(stories));

    private static WorkItemTreeCreate.Node Task(PlannedStory story, CapturePlanTask task)
        => new(
            WorkItemTypeId.Task, task.Title!,
            Changes(task.Brief, PlanTags.Format(new PlanTaskTags(story.Wave, story.Story.Lane, task.Model, task.ContextK))) with
            {
                Points = task.Points is { } points ? new FieldSet<decimal?>(points) : null,
            },
            []);

    private static WorkItemChanges Changes(string? description, IReadOnlyList<string> tags)
        => new()
        {
            Description = description is null ? null : new FieldSet<string>(description),
            Tags = new FieldSet<IReadOnlyList<string>>(tags),
        };
}
