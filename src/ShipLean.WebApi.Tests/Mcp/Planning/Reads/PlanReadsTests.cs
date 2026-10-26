using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Mcp.Planning.Reads;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Plans;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Reads;

// IPlanReads over plans seeded with PlanTreeSeed into SHIP. Each test seeds its own plan, so what one changes is not seen by another.
[TestFixture]
public class PlanReadsTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    private async Task<PlanSeedKeys> SeedAsync()
    {
        Host.ActAs(SignedInAs.Id);
        return await PlanTreeSeed.SeedAsync(Host.Services, Project, PlanFixture.FeatureEditor);
    }

    private async Task<Guid> IdAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key });
    }

    private async Task RemoveAsync(string key, string type)
    {
        await using var connection = await OpenDatabaseAsync();
        var removed = await connection.QuerySingleAsync<Guid>("SELECT id FROM state WHERE project_id = @Project AND type = @type AND category = 'removed' AND archived_at IS NULL LIMIT 1", new { Project, type });
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @removed WHERE key = @key", new { removed, key });
    }

    private async Task<PlanTree> TreeAsync(string featureKey)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPlanReads>().TreeAsync(await IdAsync(featureKey));
    }

    [Test]
    public async Task PlanReads_ShouldOrderStoriesByWaveThenRank_SkipRemoved()
    {
        var plan = PlanFixture.FeatureEditor;
        var keys = await SeedAsync();
        var last = plan.Stories[^1];
        var first = plan.Stories[0];
        var removedTask = last.Tasks[0];
        var removedStory = plan.Stories[^2];
        await using (var connection = await OpenDatabaseAsync())
        {
            // The last story (the latest rank) moves to the first wave: it must sort after that wave's earlier story, before the next wave.
            await connection.ExecuteAsync("UPDATE work_item SET tags = @tags WHERE key = @key", new { tags = PlanTags.Format(new PlanTaskTags(WaveLabel.Parse("0.1"))).ToArray(), key = keys.Stories[last.Title] });
            var removed = await connection.QuerySingleAsync<Guid>("SELECT id FROM state WHERE project_id = @Project AND type = 'task' AND category = 'removed' AND archived_at IS NULL LIMIT 1", new { Project });
            await connection.ExecuteAsync("UPDATE work_item SET state_id = @removed WHERE key = @key", new { removed, key = keys.Tasks[removedTask.Title] });
        }
        await RemoveAsync(keys.Stories[removedStory.Title], "story");

        var tree = await TreeAsync(keys.Feature);

        tree.Stories.Select(s => s.Title).ShouldBe([first.Title, last.Title, .. plan.Stories.Skip(1).Take(plan.Stories.Count - 3).Select(s => s.Title)]);
        tree.Stories.Select(s => s.Wave).ShouldBe(tree.Stories.Select(s => s.Wave).Order());
        tree.Stories.SelectMany(s => s.Tasks).Select(t => t.Title).ShouldNotContain(removedTask.Title);
        tree.Stories.Select(s => s.Title).ShouldNotContain(removedStory.Title);
        tree.Stories.SelectMany(s => s.Tasks).Select(t => t.Title).Intersect(removedStory.Tasks.Select(t => t.Title)).ShouldBeEmpty();
        tree.Stories.SelectMany(s => s.Tasks).Count().ShouldBe(plan.Stories.Sum(s => s.Tasks.Count) - 1 - removedStory.Tasks.Count);
    }

    [Test]
    public async Task PlanReads_StoryWithoutWaveTag_ShouldBeItsOwnWaveAfterTaggedOnes()
    {
        var plan = PlanFixture.FeatureEditor;
        var keys = await SeedAsync();
        Guid task;
        await using (var connection = await OpenDatabaseAsync())
        {
            var feature = await IdAsync(keys.Feature);
            // Inserted out of rank order: the waves follow rank, not insertion.
            await new WorkItemBuilder(Project).WithType("story").WithTitle("By hand, second").WithParent(feature).WithBacklogOrder("y").InsertAsync(connection);
            var story = await new WorkItemBuilder(Project).WithType("story").WithTitle("By hand, first").WithParent(feature).WithBacklogOrder("x").InsertAsync(connection);
            task = await new WorkItemBuilder(Project).WithType("task").WithTitle("By hand task").WithParent(story).InsertAsync(connection);
        }

        var tree = await TreeAsync(keys.Feature);

        tree.Stories.Select(s => s.Title).ShouldBe([.. plan.Stories.Select(s => s.Title), "By hand, first", "By hand, second"]);
        tree.Stories.TakeLast(2).Select(s => s.Wave.Text).ShouldBe(["3", "4"]);
        tree.Stories[^2].Tasks.Select(t => t.Title).ShouldBe(["By hand task"]);
        using var scope = Host.Services.CreateScope();
        var (ofTask, _) = await scope.ServiceProvider.GetRequiredService<IPlanReads>().TreeOfAsync(task);
        ofTask.Feature.Key.ShouldBe(keys.Feature);
    }

    [Test]
    public async Task PlanReads_NoStoryHasWaveTag_ShouldNumberWavesFromOne()
    {
        Guid feature;
        await using (var connection = await OpenDatabaseAsync())
        {
            feature = await new WorkItemBuilder(Project).WithType("feature").WithTitle("Planned by hand").InsertAsync(connection);
            await new WorkItemBuilder(Project).WithType("story").WithTitle("Step one").WithParent(feature).WithBacklogOrder("x").InsertAsync(connection);
            await new WorkItemBuilder(Project).WithType("story").WithTitle("Step two").WithParent(feature).WithBacklogOrder("y").InsertAsync(connection);
        }
        using var scope = Host.Services.CreateScope();

        var tree = await scope.ServiceProvider.GetRequiredService<IPlanReads>().TreeAsync(feature);

        tree.Stories.Select(s => (s.Title, s.Wave.Text)).ShouldBe([("Step one", "1"), ("Step two", "2")]);
    }

    [Test]
    public async Task PlanReads_TreeOfTaskOrStory_ShouldFindTheFeature()
    {
        var plan = PlanFixture.FeatureEditor;
        var keys = await SeedAsync();
        var story = plan.Stories[2];
        var task = story.Tasks[0];
        using var scope = Host.Services.CreateScope();
        var reads = scope.ServiceProvider.GetRequiredService<IPlanReads>();

        var fromTask = await reads.TreeOfAsync(await IdAsync(keys.Tasks[task.Title]));
        var fromStory = await reads.TreeOfAsync(await IdAsync(keys.Stories[story.Title]));
        var fromFeature = await reads.TreeOfAsync(await IdAsync(keys.Feature));

        fromTask.Tree.Feature.Key.ShouldBe(keys.Feature);
        fromTask.ItemId.ShouldBe(await IdAsync(keys.Tasks[task.Title]));
        fromStory.Tree.Feature.Key.ShouldBe(keys.Feature);
        fromStory.ItemId.ShouldBe(await IdAsync(keys.Stories[story.Title]));
        fromFeature.Tree.Feature.Key.ShouldBe(keys.Feature);
        fromFeature.ItemId.ShouldBe(fromFeature.Tree.Feature.Id);
        fromTask.Tree.Stories.Single(s => s.Title == story.Title).Tasks.Select(t => t.Title).ShouldBe(story.Tasks.Select(t => t.Title));
    }

    [Test]
    public async Task PlanReads_TreeOfItemOutsideAPlan_ShouldBeValidation()
    {
        await SeedAsync();
        Guid looseTask, emptyFeature;
        await using (var connection = await OpenDatabaseAsync())
        {
            looseTask = await new WorkItemBuilder(Project).WithType("task").WithTitle("Loose task").InsertAsync(connection);
            emptyFeature = await new WorkItemBuilder(Project).WithType("feature").WithTitle("No plan yet").InsertAsync(connection);
        }
        using var scope = Host.Services.CreateScope();
        var reads = scope.ServiceProvider.GetRequiredService<IPlanReads>();

        foreach (var id in new[] { looseTask, emptyFeature })
        {
            var error = await Should.ThrowAsync<ApiException>(() => reads.TreeOfAsync(id));
            error.Code.ShouldBe("validation");
            error.Field.ShouldBe("item");
        }
        var unknown = await Should.ThrowAsync<ApiException>(() => reads.TreeOfAsync(Guid.NewGuid()));
        unknown.Code.ShouldBe("not_found");
    }

    [Test]
    public async Task PlanReads_RemovedFeature_ShouldBeValidation()
    {
        var plan = PlanFixture.FeatureEditor;
        var keys = await SeedAsync();
        await RemoveAsync(keys.Feature, "feature");
        var featureId = await IdAsync(keys.Feature);
        using var scope = Host.Services.CreateScope();
        var reads = scope.ServiceProvider.GetRequiredService<IPlanReads>();

        var ids = new[] { featureId, await IdAsync(keys.Stories[plan.Stories[2].Title]), await IdAsync(keys.Tasks[plan.Stories[2].Tasks[0].Title]) };
        foreach (var id in ids)
        {
            var ofItem = await Should.ThrowAsync<ApiException>(() => reads.TreeOfAsync(id));
            ofItem.Code.ShouldBe("validation");
            ofItem.Field.ShouldBe("item");
        }
        var direct = await Should.ThrowAsync<ApiException>(() => reads.TreeAsync(featureId));
        direct.Code.ShouldBe("validation");
        direct.Field.ShouldBe("item");
    }
}
