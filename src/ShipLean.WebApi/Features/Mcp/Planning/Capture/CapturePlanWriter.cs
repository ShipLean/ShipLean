using Agoda.IoC.Core;
using ShipLean.Application.Features.Mcp.Planning.Reads;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;
using ShipLean.WebApi.Features.Mcp.Writes;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// Applies a previewed plan, at most once per preview. The tree and the claim on the plan id are one transaction; the branch:
// tag patch and the attachments sit outside it, so the steps are ordered to be safe to retry:
//  - a new Feature: the tree and the claim, then the attachments.
//  - an existing Feature: the tree and the claim, then the patch (goal, description, branch: tag), then the attachments. The
//    patch waits for the claim, so a plan the claim refuses (another plan won the Feature) never touches the Feature.
// A plan id that is already claimed (a retry after a failure part-way, a lost response, a second call at once) writes no tree: it
// finishes what is missing (the patch when the Feature lacks the plan's branch: tag, the files the Feature lacks), so it never
// overwrites what a person changed since, and returns the plan the first apply wrote.
[RegisterPerRequest]
public sealed class CapturePlanWriter(IWorkItemTreeWriter trees, IWorkItemStore store, StaleWrites stale, CapturePlanAttacher attacher, CapturePlanClaims claims, IPlanReads plans)
{
    public async Task<CapturePlanApplied> ApplyAsync(Guid planId, Guid projectId, CapturePlanInput input, CapturePlanTarget? existing, CancellationToken cancellationToken)
    {
        var stories = CapturePlanMapper.Sort(input);
        var options = WriteOptions.User with { InTransaction = claims.InTransaction(planId, projectId, existing?.Key, existing?.Id, stories.Count) };
        try
        {
            if (existing is null)
            {
                var created = (await trees.CreateTreeAsync(new WorkItemTreeCreate(projectId, null, [CapturePlanMapper.NewFeature(input, stories)]), options, cancellationToken)).Items.Single();
                var files = await attacher.AttachAsync(created.Id, input, cancellationToken);
                return new CapturePlanApplied(created.Key, Captured(stories, created.Children), files);
            }

            var outcome = await trees.CreateTreeAsync(new WorkItemTreeCreate(projectId, existing.Id, CapturePlanMapper.Stories(stories)), options, cancellationToken);
            await PatchFeatureAsync(existing.Id, input, cancellationToken);
            var attached = await attacher.AttachAsync(existing.Id, input, cancellationToken);
            return new CapturePlanApplied(existing.Key, Captured(stories, outcome.Items), attached);
        }
        catch (Exception exception) when (CapturePlanClaims.IsAlreadyClaimed(exception))
        {
            // Another call claimed this plan id between the caller's check and the commit: it wrote the tree, this one does not.
            var claimed = await claims.FindAsync(planId, projectId, cancellationToken);
            return claimed is { } featureId ? await FinishAsync(featureId, input, cancellationToken) : throw new InvalidOperationException("The plan claim conflicted but cannot be read.", exception);
        }
    }

    // The plan a claimed plan id wrote under featureId: the tree read back, then what the first apply may not have finished. The
    // read comes first so a Feature removed (or stripped of its plan) since the apply is refused before anything is written to it.
    public async Task<CapturePlanApplied> FinishAsync(Guid featureId, CapturePlanInput input, CancellationToken cancellationToken)
    {
        PlanTree tree;
        try
        {
            tree = await plans.TreeAsync(featureId, cancellationToken);
        }
        catch (ApiException gone) when (gone.Code == ApiErrorCodes.Validation)
        {
            throw ApiException.Validation("The Feature this plan was applied under has since been removed or lost its plan; preview again.", "feature.key");
        }

        if (input.Feature?.Key is { Length: > 0 } && tree.Feature.SharedBranch != input.SharedBranch)
        {
            await FinishPatchAsync(featureId, input, cancellationToken);
        }
        var files = await attacher.AttachMissingAsync(featureId, input, cancellationToken);
        return new CapturePlanApplied(tree.Feature.Key, [.. tree.Stories.Select(story => new CapturedStory(
            story.Key, story.Title, story.Wave.Text, story.Lane, [.. story.Tasks.Select(task => new CapturedTask(task.Key, task.Title))]))], files);
    }

    // The Feature read as it is now, after the tree: its branch: tag replaced and the person's other tags kept; goal and
    // description only when given, never the title.
    private async Task PatchFeatureAsync(Guid featureId, CapturePlanInput input, CancellationToken cancellationToken)
    {
        var feature = await store.GetAsync(featureId, cancellationToken);
        await stale.PatchAsync(feature.Id, feature.Key, FeatureChanges(input, feature.Tags), feature.Revision, false, cancellationToken);
    }

    // The patch the first apply may not have made. A second call at once can find the first one still patching: a conflict is
    // fine when the Feature now carries the plan's branch: tag, since the first call wrote the same fields.
    private async Task FinishPatchAsync(Guid featureId, CapturePlanInput input, CancellationToken cancellationToken)
    {
        try
        {
            await PatchFeatureAsync(featureId, input, cancellationToken);
        }
        catch (ApiException conflict) when (conflict.Code == ApiErrorCodes.Conflict)
        {
            if (PlanTags.Parse((await store.GetAsync(featureId, cancellationToken)).Tags).Branch != input.SharedBranch)
            {
                throw;
            }
        }
    }

    private static WorkItemChanges FeatureChanges(CapturePlanInput input, IReadOnlyList<string> tags)
        => new()
        {
            Tags = new FieldSet<IReadOnlyList<string>>([.. tags.Where(tag => PlanTags.Parse([tag]).Branch is null), .. PlanTags.Format(new PlanTaskTags(Branch: input.SharedBranch))]),
            Goal = input.Feature!.Goal is { } goal ? new FieldSet<string>(goal) : null,
            Description = input.Feature.Description is { } description ? new FieldSet<string>(description) : null,
        };

    private static IReadOnlyList<CapturedStory> Captured(IReadOnlyList<PlannedStory> stories, IReadOnlyList<WorkItemTreeOutcome.Node> written)
        => [.. stories.Zip(written, (planned, node) => new CapturedStory(
            node.Key, planned.Story.Title!, planned.Wave.Text, planned.Story.Lane,
            [.. (planned.Story.Tasks ?? []).Zip(node.Children, (task, child) => new CapturedTask(child.Key, task.Title!))]))];
}
