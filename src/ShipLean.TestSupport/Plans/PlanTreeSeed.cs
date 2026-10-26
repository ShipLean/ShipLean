using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.TestSupport.Plans;

// Seeds a plan fixture the way capture_plan lands it: one tree through IWorkItemTreeWriter (the Feature with its branch: tag
// and the fixture's goal in the Goal field and its description as the description, the stories in the fixture's order, their tasks with the
// task brief as description), then common-brief.md, and PLAN.md when the fixture has plan markdown, attached to the Feature.
public static class PlanTreeSeed
{
    // Writes as ICurrentUser, which outside a request is whoever Host.ActAs last named, so the caller must act as a member of the
    // project first: Host.ActAs(SeedIds.Users.Joel). ApiFixture does that in [SetUp] only. In [OneTimeSetUp] nobody is acting yet,
    // and McpFixture signs in as TestUser.Anonymous, so [SetUp] resets it to nobody: in both cases call Host.ActAs(SeedIds.Users.Joel)
    // before seeding, or the tree writer throws "No signed-in user".
    public static async Task<PlanSeedKeys> SeedAsync(IServiceProvider services, Guid projectId, PlanFixture plan, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var feature = new WorkItemTreeCreate.Node(
            WorkItemTypeId.Feature, plan.Feature.Title,
            Changes(plan.Feature.Description, plan.FeatureTags) with { Goal = new FieldSet<string>(plan.Feature.Goal) },
            [.. plan.Stories.Select(story => new WorkItemTreeCreate.Node(
                WorkItemTypeId.Story, story.Title, Changes(story.Description, story.Tags),
                [.. story.Tasks.Select(task => new WorkItemTreeCreate.Node(WorkItemTypeId.Task, task.Title, Changes(task.Brief, task.Tags), []))]))]);

        var outcome = await scope.ServiceProvider.GetRequiredService<IWorkItemTreeWriter>()
            .CreateTreeAsync(new WorkItemTreeCreate(projectId, null, [feature]), WriteOptions.User, cancellationToken);

        var root = outcome.Items.Single();
        var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentStore>();
        await Attach(attachments, root.Id, PlanFiles.CommonBrief, plan.CommonBrief, cancellationToken);
        if (plan.PlanMarkdown is not null)
        {
            await Attach(attachments, root.Id, PlanFiles.Plan, plan.PlanMarkdown, cancellationToken);
        }

        var stories = plan.Stories.Zip(root.Children).ToList();
        var tasks = stories.SelectMany(pair => pair.First.Tasks.Zip(pair.Second.Children)).ToList();
        var ids = new[] { root }.Concat(stories.Select(pair => pair.Second)).Concat(tasks.Select(pair => pair.Second)).ToDictionary(node => node.Key, node => node.Id);
        return new PlanSeedKeys(
            root.Key,
            stories.ToDictionary(pair => pair.First.Title, pair => pair.Second.Key),
            tasks.ToDictionary(pair => pair.First.Title, pair => pair.Second.Key),
            ids);
    }

    private static async Task Attach(IAttachmentStore attachments, Guid itemId, string name, string markdown, CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        await attachments.AddAsync(itemId, name, "text/markdown", content, cancellationToken: cancellationToken);
    }

    private static WorkItemChanges Changes(string description, PlanTaskTags tags)
        => new() { Description = new FieldSet<string>(description), Tags = new FieldSet<IReadOnlyList<string>>(PlanTags.Format(tags)) };
}
