using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.TestSupport.Contracts.WorkItems;

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
            new WorkTreeRollupDto(outer, WorkItemTypeId.Feature, 1, 0, null, null, 1, 0, 0),
            new WorkTreeRollupDto(outer, WorkItemTypeId.Story, 2, 1, 7m, 15m, 0, 0, 0),
            new WorkTreeRollupDto(outer, WorkItemTypeId.Bug, 1, 0, null, null, 1, 0, 0),
        ], ignoreOrder: true);
        (await Reads.RollupsAsync(project.ProjectId, [epic], null)).Single(r => r.Type == WorkItemTypeId.Story).ShouldBe(
            new WorkTreeRollupDto(epic, WorkItemTypeId.Story, 2, 1, 7m, 15m, 0, 0, 0));
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

        rollups.ShouldBe([new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 1, 1, 3m, 8m, 0, 0, 0)]);
    }

    [Test]
    public async Task Rollup_WhenNothingEstimated_ShouldHaveNullPointsAndCountUnestimated()
    {
        var project = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        await AddAsync(project, "story", b => b.WithParent(feature));
        await AddAsync(project, "story", b => b.WithParent(feature).WithState(Closed(project, "story")));

        var rollups = await Reads.RollupsAsync(project.ProjectId, [feature], null);

        rollups.ShouldBe([new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 1, 1, null, null, 2, 0, 0)]);
    }

    [Test]
    public async Task Rollup_WhenOnlyClosedWorkIsEstimated_ShouldHaveNullOpenPoints()
    {
        var project = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        await AddAsync(project, "story", b => b.WithParent(feature).WithPoints(5).WithState(Closed(project, "story")));
        await AddAsync(project, "story", b => b.WithParent(feature));

        var rollups = await Reads.RollupsAsync(project.ProjectId, [feature], null);

        rollups.ShouldBe([new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 1, 1, null, 5m, 1, 0, 0)]);
    }

    [Test]
    public async Task Rollup_WhenParentRemoved_ShouldStillCountLiveChildren()
    {
        var project = await ArrangeProjectAsync();
        var epic = await AddAsync(project, "epic");
        var removed = await AddAsync(project, "feature", b => b.WithParent(epic).WithState(Removed(project, "feature")));
        await AddAsync(project, "story", b => b.WithParent(removed).WithPoints(3));

        var rollups = await Reads.RollupsAsync(project.ProjectId, [epic], null);

        rollups.ShouldBe([new WorkTreeRollupDto(epic, WorkItemTypeId.Story, 1, 0, 3m, 3m, 0, 0, 0)]);
    }

    [Test]
    public async Task Rollup_WhenParentsCycle_ShouldNotListARootAmongItsOwnDescendants()
    {
        var project = await ArrangeProjectAsync();
        var one = await AddAsync(project, "feature");
        var two = await AddAsync(project, "feature", b => b.WithParent(one));
        await SetParentAsync(one, two);

        var rollups = await Reads.RollupsAsync(project.ProjectId, [one], null);

        rollups.ShouldBe([new WorkTreeRollupDto(one, WorkItemTypeId.Feature, 1, 0, null, null, 1, 0, 0)]);
    }

    [Test]
    public async Task Rollups_WhenOpenStoryInCurrentSprint_ShouldCountInCurrentSprint()
    {
        var (project, feature) = await ArrangeFeatureAsync();
        var sprint = await AddSprintAsync(project, "Running", -3, 3);
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(sprint));

        (await StoryRollupAsync(project, feature)).ShouldBe(new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 2, 0, null, null, 2, 1, 0));
    }

    [Test]
    public async Task Rollups_WhenOpenStoryInEndedUndecidedSprint_ShouldCountInCurrentSprint()
    {
        var (project, feature) = await ArrangeFeatureAsync();
        var undecided = await AddSprintAsync(project, "Ended, not decided", -20, -7);
        var pending = await AddSprintAsync(project, "Ended, pending", -34, -21, rolloverStatus: "pending");
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(undecided));
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(pending));

        (await StoryRollupAsync(project, feature)).ShouldBe(new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 3, 0, null, null, 3, 2, 0));
    }

    [Test]
    public async Task Rollups_WhenOpenStoryInEndedDecidedSprint_ShouldCountInOtherSprint()
    {
        var (project, feature) = await ArrangeFeatureAsync();
        var decided = await AddSprintAsync(project, "Ended, decided", -20, -7, rolloverStatus: "decided");
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(decided));

        (await StoryRollupAsync(project, feature)).ShouldBe(new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 2, 0, null, null, 2, 0, 1));
    }

    [Test]
    public async Task Rollups_WhenOpenStoryInFutureSprint_ShouldCountInOtherSprint()
    {
        var (project, feature) = await ArrangeFeatureAsync();
        var future = await AddSprintAsync(project, "Next", 4, 17);
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(future));

        (await StoryRollupAsync(project, feature)).ShouldBe(new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 2, 0, null, null, 2, 0, 1));
    }

    [Test]
    public async Task Rollups_WhenOpenStoryInArchivedSprint_ShouldCountInNeither()
    {
        var (project, feature) = await ArrangeFeatureAsync();
        var archivedCurrent = await AddSprintAsync(project, "Archived, running", -3, 3, archived: true);
        var archivedFuture = await AddSprintAsync(project, "Archived, next", 4, 17, archived: true);
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(archivedCurrent));
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(archivedFuture));

        (await StoryRollupAsync(project, feature)).ShouldBe(new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 3, 0, null, null, 3, 0, 0));
    }

    [Test]
    public async Task Rollups_WhenClosedStoryInCurrentSprint_ShouldCountOnlyAsClosed()
    {
        var (project, feature) = await ArrangeFeatureAsync();
        var sprint = await AddSprintAsync(project, "Running", -3, 3);
        var future = await AddSprintAsync(project, "Next", 4, 17);
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(sprint).WithState(Closed(project, "story")));
        await AddAsync(project, "story", b => b.WithParent(feature).WithSprint(future).WithState(Closed(project, "story")));

        (await StoryRollupAsync(project, feature)).ShouldBe(new WorkTreeRollupDto(feature, WorkItemTypeId.Story, 1, 2, null, null, 3, 0, 0));
    }

    // A Feature in a project of its own with one open story in no sprint, so every case also shows that no sprint is neither.
    private async Task<(ArrangedProject Project, Guid Feature)> ArrangeFeatureAsync()
    {
        var project = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        await AddAsync(project, "story", b => b.WithParent(feature));
        return (project, feature);
    }

    private async Task<WorkTreeRollupDto> StoryRollupAsync(ArrangedProject project, Guid feature) =>
        (await Reads.RollupsAsync(project.ProjectId, [feature], null)).Single(r => r.Type == WorkItemTypeId.Story);

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
