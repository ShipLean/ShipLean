using System.Net;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard;

[TestFixture]
public class FeatureBoardReadApiTests : ApiFixture
{
    private static WorkItemBuilder Item(string type, string title) => new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithTitle(title);

    private static Task<string> KeyAsync(NpgsqlConnection connection, Guid id) => connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id })!;

    private async Task<FeatureBoardViewDto> ViewAsync(NpgsqlConnection connection, Guid feature)
        => await Api.GetAsync<FeatureBoardViewDto>($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{await KeyAsync(connection, feature)}/feature-board");

    [Test]
    public async Task FeatureBoard_WhenLeafFeatureHasStoriesInSprintsAndBacklog_ShouldListThemAsRowsInOneSection()
    {
        await using var connection = await OpenDatabaseAsync();
        var late = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, "FB late", 900);
        var early = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, "FB early", 700);
        var feature = await Item("feature", "Leaf feature").InsertAsync(connection);
        var inLate = await Item("story", "In late").WithParent(feature).WithSprint(late).WithPoints(3).InsertAsync(connection);
        var inEarly = await Item("bug", "In early").WithParent(feature).WithSprint(early).WithPoints(2).WithStateNamed("Closed").InsertAsync(connection);
        var backlog = await Item("story", "In backlog").WithParent(feature).InsertAsync(connection);
        var taskA = await Item("task", "Task A").WithParent(inLate).InsertAsync(connection);
        var taskB = await Item("task", "Task B").WithParent(inEarly).WithStateNamed("Closed").InsertAsync(connection);
        var removedTask = await Item("task", "Removed task").WithParent(inLate).WithStateNamed("Removed").InsertAsync(connection);
        var removedStory = await Item("story", "Removed story").WithParent(feature).WithStateNamed("Removed").InsertAsync(connection);

        var view = await ViewAsync(connection, feature);

        view.FeatureId.ShouldBe(feature);
        view.BoardId.ShouldBe(SeedIds.Boards.ShipTaskboard);
        view.Sections.Select(s => s.Id).ShouldBe([feature]);
        view.Rows.Select(r => r.Parent.Id).ShouldBe([inLate, backlog, inEarly], ignoreOrder: true);
        view.Rows.Select(r => r.Parent.Id).ShouldNotContain(removedStory);
        view.Rows.ShouldAllBe(r => r.FeatureId == feature && r.Cells.Count == view.Columns.Count);
        var tasks = view.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Tasks).Select(t => t.Id).ToList();
        tasks.ShouldBe([taskA, taskB], ignoreOrder: true);
        tasks.ShouldNotContain(removedTask);
        view.Rows.Single(r => r.Parent.Id == backlog).Sprint.ShouldBeNull();
        view.Rows.Single(r => r.Parent.Id == backlog).Cells.SelectMany(c => c.Tasks).ShouldBeEmpty();
        view.Rows.Single(r => r.Parent.Id == inEarly).Sprint!.Id.ShouldBe(early);
        view.PeopleRows[0].Person.ShouldBeNull();
        view.PeopleRows.Sum(p => p.TaskCount).ShouldBe(2);
    }

    [Test]
    public async Task FeatureBoard_WhenLoaded_ShouldNumberHeaderAndSectionFromTheRows()
    {
        await using var connection = await OpenDatabaseAsync();
        var late = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, "FB num late", 910);
        var early = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, "FB num early", 710);
        var feature = await Item("feature", "Numbers feature").InsertAsync(connection);
        var open = await Item("story", "Open").WithParent(feature).WithSprint(late).WithPoints(3).InsertAsync(connection);
        var closed = await Item("story", "Closed").WithParent(feature).WithSprint(early).WithPoints(2).WithStateNamed("Closed").InsertAsync(connection);
        await Item("story", "Backlog").WithParent(feature).InsertAsync(connection);
        await Item("task", "Open task").WithParent(open).InsertAsync(connection);
        await Item("task", "Closed task").WithParent(closed).WithStateNamed("Closed").InsertAsync(connection);

        var view = await ViewAsync(connection, feature);

        view.Header.ShouldSatisfyAllConditions(
            h => h.Key.ShouldBe(view.Sections[0].Key),
            h => h.FeaturesUnder.ShouldBe(0),
            h => h.StoriesClosed.ShouldBe(1),
            h => h.StoriesTotal.ShouldBe(3),
            h => h.PointsClosed.ShouldBe(2),
            h => h.PointsTotal.ShouldBe(5),
            h => h.TasksClosed.ShouldBe(1),
            h => h.TasksTotal.ShouldBe(2),
            h => h.AnyInBacklog.ShouldBeTrue());
        view.Header.Sprints.Select(s => s.Id).ShouldBe([early, late]);
        view.Sections[0].StoriesTotal.ShouldBe(3);
        view.Sections[0].PointsTotal.ShouldBe(5);
        view.Columns.Sum(c => c.Count).ShouldBe(2);
    }

    [Test]
    public async Task FeatureBoard_WhenNoStoryInBacklog_ShouldNotFlagBacklog()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, "FB no backlog", 920);
        var feature = await Item("feature", "No backlog feature").InsertAsync(connection);
        await Item("story", "Planned").WithParent(feature).WithSprint(sprint).InsertAsync(connection);

        var view = await ViewAsync(connection, feature);

        view.Header.AnyInBacklog.ShouldBeFalse();
        view.Header.PointsTotal.ShouldBeNull();
        view.Header.PointsClosed.ShouldBeNull();
    }

    [Test]
    public async Task FeatureBoard_WhenParentFeatureHasNestedFeatures_ShouldOrderSectionsDepthFirstOwnStoriesFirst()
    {
        await using var connection = await OpenDatabaseAsync();
        var root = await Item("feature", "Root feature").InsertAsync(connection);
        var childA = await Item("feature", "Child A").WithParent(root).InsertAsync(connection);
        var grandchild = await Item("feature", "Grandchild").WithParent(childA).InsertAsync(connection);
        var childB = await Item("feature", "Child B").WithParent(root).InsertAsync(connection);
        var rootStory = await Item("story", "Root story").WithParent(root).InsertAsync(connection);
        var grandStory = await Item("story", "Grand story").WithParent(grandchild).InsertAsync(connection);
        var aStory = await Item("bug", "A bug").WithParent(childA).InsertAsync(connection);
        var emptyBStory = await Item("story", "B story").WithParent(childB).InsertAsync(connection);

        var view = await ViewAsync(connection, root);

        view.Sections.Select(s => s.Id).ShouldBe([root, childA, grandchild, childB]);
        view.Sections.Select(s => s.Depth).ShouldBe([0, 1, 2, 1]);
        view.Sections.Select(s => s.ParentFeature?.Id).ShouldBe([null, null, childA, null]);
        view.Header.FeaturesUnder.ShouldBe(3);
        view.Rows.Select(r => r.Parent.Id).ShouldBe([rootStory, aStory, grandStory, emptyBStory]);
        view.Rows.Select(r => r.FeatureId).ShouldBe([root, childA, grandchild, childB]);
        view.Sections.Select(s => s.StoriesTotal).ShouldBe([1, 1, 1, 1]);
        view.Header.StoriesTotal.ShouldBe(4);
    }

    [Test]
    public async Task FeatureBoard_WhenNestedFeatureIsRemoved_ShouldPutItsStoriesInTheRootSectionAndLiftItsFeatures()
    {
        await using var connection = await OpenDatabaseAsync();
        var root = await Item("feature", "Removal root").InsertAsync(connection);
        var removed = await Item("feature", "Removed nested").WithParent(root).WithStateNamed("Removed").InsertAsync(connection);
        var orphanStory = await Item("story", "Orphaned story").WithParent(removed).InsertAsync(connection);
        var lifted = await Item("feature", "Lifted nested").WithParent(removed).InsertAsync(connection);
        var liftedStory = await Item("story", "Lifted story").WithParent(lifted).InsertAsync(connection);

        var view = await ViewAsync(connection, root);

        view.Sections.Select(s => s.Id).ShouldBe([root, lifted]);
        view.Sections.Select(s => s.Depth).ShouldBe([0, 1]);
        view.Rows.Select(r => (r.Parent.Id, r.FeatureId)).ShouldBe([(orphanStory, root), (liftedStory, lifted)]);
    }

    [Test]
    public async Task FeatureBoard_WhenKeyIsAStory_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await Item("feature", "Has a story").InsertAsync(connection);
        var story = await Item("story", "Just a story").WithParent(feature).InsertAsync(connection);

        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{await KeyAsync(connection, story)}/feature-board");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task FeatureBoard_WhenFeatureIsRemoved_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await Item("feature", "Gone feature").WithStateNamed("Removed").InsertAsync(connection);

        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{await KeyAsync(connection, feature)}/feature-board");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task FeatureBoard_WhenNotMember_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Part}/work-items/by-key/SHIP-103/feature-board");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
