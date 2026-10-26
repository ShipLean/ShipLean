using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.WebApi.Tests.WorkItems.Reads.Tree;

[TestFixture]
public class WorkTreeRollupTests : WorkTreeReadsTestBase
{
    [Test]
    public async Task Rollup_WhenNestedFeature_ShouldCountGrandchildren()
    {
        var project = await ArrangeProjectAsync();
        var epic = await AddAsync(project, "epic");
        var outer = await AddAsync(project, "feature", b => b.WithParent(epic));
        var inner = await AddAsync(project, "feature", b => b.WithParent(outer));
        await AddAsync(project, "story", b => b.WithParent(outer).WithPoints(2));
        await AddAsync(project, "story", b => b.WithParent(inner).WithPoints(5));
        await AddAsync(project, "story", b => b.WithParent(inner).WithPoints(8).WithState(Closed(project, "story")));
        await AddAsync(project, "bug", b => b.WithParent(inner));
        await AddAsync(project, "story", b => b.WithParent(inner).WithPoints(100).WithState(Removed(project, "story")));

        var rollups = await Reads.RollupsAsync(project.ProjectId, [outer], null);

        rollups.ShouldBe(
        [
            new WorkTreeRollupDto(outer, WorkItemTypeId.Feature, 1, 0, null, null, 1),
            new WorkTreeRollupDto(outer, WorkItemTypeId.Story, 2, 1, 7m, 15m, 0),
            new WorkTreeRollupDto(outer, WorkItemTypeId.Bug, 1, 0, null, null, 1),
        ], ignoreOrder: true);
        (await Reads.RollupsAsync(project.ProjectId, [epic], null)).Single(r => r.Type == WorkItemTypeId.Story).ShouldBe(
            new WorkTreeRollupDto(epic, WorkItemTypeId.Story, 2, 1, 7m, 15m, 0));
    }

    [Test]
    public async Task Rollup_WhenFilterGiven_ShouldCountOnlyPassingChildren()
    {
        var project = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        await AddAsync(project, "story", b => b.WithParent(feature).WithPoints(3).WithTags("keep"));
        await AddAsync(project, "story", b => b.WithParent(feature).WithPoints(5).WithTags("keep").WithState(Closed(project, "story")));
        await AddAsync(project, "story", b => b.WithParent(feature).WithPoints(13).WithTags("other"));

        var rollups = await Reads.RollupsAsync(project.ProjectId, [feature], Where(FieldIds.Tags, CriteriaOp.Contains, "keep"));

        rollups.ShouldBe([new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 1, 1, 3m, 8m, 0)]);
    }

    [Test]
    public async Task Rollup_WhenNothingEstimated_ShouldHaveNullPointsAndCountUnestimated()
    {
        var project = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        await AddAsync(project, "story", b => b.WithParent(feature));
        await AddAsync(project, "story", b => b.WithParent(feature).WithState(Closed(project, "story")));

        var rollups = await Reads.RollupsAsync(project.ProjectId, [feature], null);

        rollups.ShouldBe([new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 1, 1, null, null, 2)]);
    }

    [Test]
    public async Task Rollup_WhenOnlyClosedWorkIsEstimated_ShouldHaveNullOpenPoints()
    {
        var project = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        await AddAsync(project, "story", b => b.WithParent(feature).WithPoints(5).WithState(Closed(project, "story")));
        await AddAsync(project, "story", b => b.WithParent(feature));

        var rollups = await Reads.RollupsAsync(project.ProjectId, [feature], null);

        rollups.ShouldBe([new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 1, 1, null, 5m, 1)]);
    }

    [Test]
    public async Task Rollup_WhenParentRemoved_ShouldStillCountLiveChildren()
    {
        var project = await ArrangeProjectAsync();
        var epic = await AddAsync(project, "epic");
        var removed = await AddAsync(project, "feature", b => b.WithParent(epic).WithState(Removed(project, "feature")));
        await AddAsync(project, "story", b => b.WithParent(removed).WithPoints(3));

        var rollups = await Reads.RollupsAsync(project.ProjectId, [epic], null);

        rollups.ShouldBe([new WorkTreeRollupDto(epic, WorkItemTypeId.Story, 1, 0, 3m, 3m, 0)]);
    }

    [Test]
    public async Task Rollup_WhenParentsCycle_ShouldNotListARootAmongItsOwnDescendants()
    {
        var project = await ArrangeProjectAsync();
        var one = await AddAsync(project, "feature");
        var two = await AddAsync(project, "feature", b => b.WithParent(one));
        await SetParentAsync(one, two);

        var rollups = await Reads.RollupsAsync(project.ProjectId, [one], null);

        rollups.ShouldBe([new WorkTreeRollupDto(one, WorkItemTypeId.Feature, 1, 0, null, null, 1)]);
    }

    [Test]
    public async Task Rollup_WhenProjectIsForeign_ShouldReturnNothing()
    {
        var project = await ArrangeProjectAsync();
        var other = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        await AddAsync(project, "story", b => b.WithParent(feature));

        (await Reads.RollupsAsync(other.ProjectId, [feature], null)).ShouldBeEmpty();
        (await Reads.RollupsAsync(project.ProjectId, [], null)).ShouldBeEmpty();
    }
}
