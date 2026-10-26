using ShipLean.Contracts.Refs;

namespace ShipLean.WebApi.Tests.WorkItems.Reads.Tree;

[TestFixture]
public class WorkTreeDescendantsTests : WorkTreeReadsTestBase
{
    [Test]
    public async Task Descendants_ShouldListOpenFirstThenClosed()
    {
        var project = await ArrangeProjectAsync();
        var epic = await AddAsync(project, "epic");
        var feature = await AddAsync(project, "feature", b => b.WithParent(epic).WithBacklogOrder("2"));
        var closedFirst = await AddAsync(project, "story", b => b.WithParent(feature).WithBacklogOrder("1").WithState(Closed(project, "story")));
        var openLate = await AddAsync(project, "story", b => b.WithParent(feature).WithBacklogOrder("4").WithPoints(3).WithTitle("Late"));
        var openEarly = await AddAsync(project, "bug", b => b.WithParent(feature).WithBacklogOrder("3").WithState(Active(project, "bug")));
        var task = await AddAsync(project, "task", b => b.WithParent(openLate).WithBacklogOrder("5"));

        var all = await Reads.DescendantsAsync(project.ProjectId, epic, null);

        all.Select(i => i.Id).ShouldBe([feature, openEarly, openLate, task, closedFirst]);
        var late = all.Single(i => i.Id == openLate);
        late.Title.ShouldBe("Late");
        late.Type.ShouldBe(new TypeRef(WorkItemTypeId.Story, "User Story"));
        late.State.Category.ShouldBe(StateCategory.Proposed);
        late.Points.ShouldBe(3m);
        late.ParentId.ShouldBe(feature);
        late.Depth.ShouldBe(2);
        late.Revision.ShouldBe(1);
        all.Single(i => i.Id == feature).Depth.ShouldBe(1);
        all.Single(i => i.Id == task).Depth.ShouldBe(3);
        (await Reads.DescendantsAsync(project.ProjectId, epic, WorkItemTypeId.Story)).Select(i => i.Id).ShouldBe([openLate, closedFirst]);
    }

    [Test]
    public async Task Descendants_WhenRemoved_ShouldExclude()
    {
        var project = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        var kept = await AddAsync(project, "story", b => b.WithParent(feature));
        await AddAsync(project, "story", b => b.WithParent(feature).WithState(Removed(project, "story")));

        (await Reads.DescendantsAsync(project.ProjectId, feature, null)).Select(i => i.Id).ShouldBe([kept]);
    }

    [Test]
    public async Task Descendants_WhenProjectIsForeign_ShouldReturnNothing()
    {
        var project = await ArrangeProjectAsync();
        var other = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        await AddAsync(project, "story", b => b.WithParent(feature));

        (await Reads.DescendantsAsync(other.ProjectId, feature, null)).ShouldBeEmpty();
    }
}
