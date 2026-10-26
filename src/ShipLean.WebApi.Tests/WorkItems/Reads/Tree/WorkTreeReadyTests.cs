using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Reads.Tree;

[TestFixture]
public class WorkTreeReadyTests : WorkTreeReadsTestBase
{
    [Test]
    public async Task Ready_ShouldBeUnassignedProposedInStackOrder()
    {
        var project = await ArrangeProjectAsync();
        var sprint = await AddSprintAsync(project, "S1");
        var elsewhere = await AddSprintAsync(project, "S2");
        var feature = await AddAsync(project, "feature");
        var second = await AddAsync(project, "bug", b => b.WithSprint(sprint).WithBacklogOrder("2"));
        var first = await AddAsync(project, "story", b => b.WithSprint(sprint).WithBacklogOrder("1").WithParent(feature));
        await AddAsync(project, "story", b => b.WithSprint(sprint).WithBacklogOrder("0").WithAssignee(SeedIds.Users.Joel));
        await AddAsync(project, "story", b => b.WithSprint(sprint).WithBacklogOrder("0").WithState(Active(project, "story")));
        await AddAsync(project, "story", b => b.WithSprint(sprint).WithBacklogOrder("0").WithState(Closed(project, "story")));
        await AddAsync(project, "story", b => b.WithSprint(elsewhere).WithBacklogOrder("0"));
        await AddAsync(project, "story", b => b.WithBacklogOrder("0"));

        var ready = await Reads.ReadyAsync(project.ProjectId, sprint, null, null);

        ready.Select(i => i.Id).ShouldBe([first, second]);
        ready[0].Sprint.ShouldNotBeNull().Status.ShouldBe(SprintStatus.Current);
        (await Reads.ReadyAsync(project.ProjectId, sprint, WorkItemTypeId.Bug, null)).Select(i => i.Id).ShouldBe([second]);
        (await Reads.ReadyAsync(project.ProjectId, sprint, null, feature)).Select(i => i.Id).ShouldBe([first]);
    }

    [Test]
    public async Task Ready_WhenParentIsEpic_ShouldIncludeWorkUnderItsFeatures()
    {
        var project = await ArrangeProjectAsync();
        var sprint = await AddSprintAsync(project, "S1");
        var epic = await AddAsync(project, "epic");
        var feature = await AddAsync(project, "feature", b => b.WithParent(epic));
        var story = await AddAsync(project, "story", b => b.WithSprint(sprint).WithParent(feature).WithBacklogOrder("1"));
        var task = await AddAsync(project, "task", b => b.WithSprint(sprint).WithParent(story).WithBacklogOrder("2"));
        await AddAsync(project, "story", b => b.WithSprint(sprint).WithBacklogOrder("0"));

        var underEpic = await Reads.ReadyAsync(project.ProjectId, sprint, null, epic);
        var underStory = await Reads.ReadyAsync(project.ProjectId, sprint, null, story);

        underEpic.Select(i => i.Id).ShouldBe([story, task]);
        underEpic.Select(i => i.Depth).ShouldBe([2, 3]);
        underStory.Select(i => i.Id).ShouldBe([task]);
        (await Reads.ReadyAsync(project.ProjectId, sprint, null, null)).Select(i => i.Depth).ShouldAllBe(d => d == 0);
    }

    [Test]
    public async Task Ready_WhenProjectIsForeign_ShouldReturnNothing()
    {
        var project = await ArrangeProjectAsync();
        var other = await ArrangeProjectAsync();
        var sprint = await AddSprintAsync(project, "S1");
        await AddAsync(project, "story", b => b.WithSprint(sprint));

        (await Reads.ReadyAsync(other.ProjectId, sprint, null, null)).ShouldBeEmpty();
    }
}
