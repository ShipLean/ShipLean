using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.WebApi.Tests.WorkItems.Reads.Tree;

[TestFixture]
public class WorkTreeStackTests : WorkTreeReadsTestBase
{
    [Test]
    public async Task Stack_ShouldGiveTheLevelInStackOrderWithoutRemoved()
    {
        var project = await ArrangeProjectAsync();
        var feature = await AddAsync(project, "feature");
        var second = await AddAsync(project, "bug", b => b.WithBacklogOrder("2"));
        var first = await AddAsync(project, "story", b => b.WithBacklogOrder("1").WithParent(feature).WithPoints(2).WithTitle("First").WithTags("a", "b"));
        await SetCriteriaAsync(first);
        await SetRevisionAsync(first, 4);
        await AddAsync(project, "story", b => b.WithBacklogOrder("0").WithState(Removed(project, "story")));
        await AddAsync(project, "task", b => b.WithBacklogOrder("0"));

        var rows = await Reads.StackAsync(project.ProjectId, StackLevel.Requirement, null, includeCompleted: true);

        rows.Select(r => r.Id).ShouldBe([first, second]);
        var row = rows[0];
        row.Key.ShouldNotBeEmpty();
        row.Points.ShouldBe(2m);
        row.Parent.ShouldNotBeNull().Id.ShouldBe(feature);
        row.Tags.ShouldBe(["a", "b"]);
        row.Revision.ShouldBe(4);
        rows[1].Tags.ShouldBeEmpty();
        rows[1].Revision.ShouldBe(1);
        row.HasAcceptanceCriteria.ShouldBeTrue();
        rows[1].HasAcceptanceCriteria.ShouldBeFalse();
        rows[1].ChangedAt.ShouldBeGreaterThan(TimeProvider.System.GetUtcNow().AddMinutes(-10));
        (await Reads.StackAsync(project.ProjectId, StackLevel.Feature, null, true)).Select(r => r.Id).ShouldBe([feature]);
        (await Reads.StackAsync(project.ProjectId, StackLevel.Epic, null, true)).ShouldBeEmpty();
    }

    [Test]
    public async Task Stack_WhenFilterGiven_ShouldBeParameterised()
    {
        var project = await ArrangeProjectAsync();
        var hostile = "x'); DROP TABLE work_item; --";
        var match = await AddAsync(project, "story", b => b.WithTitle(hostile));
        await AddAsync(project, "story", b => b.WithTitle("Plain"));
        var filter = Where(FieldIds.Title, CriteriaOp.Contains, hostile);

        var rows = await Reads.StackAsync(project.ProjectId, StackLevel.Requirement, filter, true);

        rows.Select(r => r.Id).ShouldBe([match]);
        (await Reads.StackAsync(project.ProjectId, StackLevel.Requirement, null, true)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Stack_WhenFilterIsInvalid_ShouldBeValidationError()
    {
        var project = await ArrangeProjectAsync();

        var error = await Should.ThrowAsync<ApiException>(() => Reads.StackAsync(project.ProjectId, StackLevel.Requirement, Where("nope", CriteriaOp.Equal, "x"), true));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Stack_WhenCompletedExcluded_ShouldOmitCompletedCategory()
    {
        var project = await ArrangeProjectAsync();
        var open = await AddAsync(project, "story", b => b.WithBacklogOrder("2"));
        var active = await AddAsync(project, "story", b => b.WithBacklogOrder("3").WithState(Active(project, "story")));
        var closed = await AddAsync(project, "story", b => b.WithBacklogOrder("1").WithState(Closed(project, "story")));

        (await Reads.StackAsync(project.ProjectId, StackLevel.Requirement, null, includeCompleted: false)).Select(r => r.Id).ShouldBe([open, active]);
        (await Reads.StackAsync(project.ProjectId, StackLevel.Requirement, null, includeCompleted: true)).Select(r => r.Id).ShouldBe([closed, open, active]);
    }

    [Test]
    public async Task Stack_WhenProjectIsForeign_ShouldReturnNothing()
    {
        var project = await ArrangeProjectAsync();
        var other = await ArrangeProjectAsync();
        await AddAsync(project, "story");

        (await Reads.StackAsync(other.ProjectId, StackLevel.Requirement, null, true)).ShouldBeEmpty();
    }
}
