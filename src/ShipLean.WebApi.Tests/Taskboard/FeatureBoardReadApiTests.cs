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
    public async Task FeatureBoard_WhenSectionHasOpenAndClosedStories_ShouldListOpenBeforeClosed()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await Item("feature", "Open then closed").InsertAsync(connection);
        var closed = await Item("story", "Closed first").WithParent(feature).WithStateNamed("Closed").InsertAsync(connection);
        var openLater = await Item("bug", "Open later").WithParent(feature).InsertAsync(connection);
        var openLast = await Item("story", "Open last").WithParent(feature).InsertAsync(connection);

        var view = await ViewAsync(connection, feature);

        var ids = view.Rows.Select(r => r.Parent.Id).ToList();
        ids.ShouldBe([openLater, openLast, closed], ignoreOrder: true);
        ids[^1].ShouldBe(closed);
    }

    [Test]
    public async Task FeatureBoard_WhenTasksAreAssigned_ShouldListUnassignedFirstThenPeopleByName()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await Item("feature", "People order").InsertAsync(connection);
        var story = await Item("story", "People story").WithParent(feature).InsertAsync(connection);
        await Item("task", "By Tom").WithParent(story).WithAssignee(SeedIds.Users.Tom).InsertAsync(connection);
        await Item("task", "By Priya").WithParent(story).WithAssignee(SeedIds.Users.Priya).InsertAsync(connection);
        await Item("task", "By Mei").WithParent(story).WithAssignee(SeedIds.Users.Mei).InsertAsync(connection);
        await Item("task", "Nobody").WithParent(story).InsertAsync(connection);

        var view = await ViewAsync(connection, feature);

        view.PeopleRows[0].Person.ShouldBeNull();
        view.PeopleRows[0].TaskCount.ShouldBe(1);
        var names = view.PeopleRows.Skip(1).Select(p => p.Person!.Name).ToList();
        names.Count.ShouldBe(3);
        names.ShouldBe([.. names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)]);
        view.PeopleRows.Skip(1).Select(p => p.Person!.Id).ShouldBe([SeedIds.Users.Tom, SeedIds.Users.Priya, SeedIds.Users.Mei], ignoreOrder: true);
    }

    [Test]
    public async Task FeatureBoard_WhenPeopleCellsMixStories_ShouldListTasksInRowOrder()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await Item("feature", "Mixed cell").InsertAsync(connection);
        var closedStory = await Item("story", "Closed story").WithParent(feature).WithStateNamed("Closed").InsertAsync(connection);
        var openStory = await Item("story", "Open story").WithParent(feature).InsertAsync(connection);
        var closedTask = await Item("task", "Under closed").WithParent(closedStory).WithAssignee(SeedIds.Users.Tom).InsertAsync(connection);
        var openTask = await Item("task", "Under open").WithParent(openStory).WithAssignee(SeedIds.Users.Tom).InsertAsync(connection);

        var view = await ViewAsync(connection, feature);

        view.Rows.Select(r => r.Parent.Id).ShouldBe([openStory, closedStory]);
        var cell = view.PeopleRows.Single(p => p.Person?.Id == SeedIds.Users.Tom).Cells.Single(c => c.Tasks.Count == 2);
        cell.Tasks.Select(t => t.Id).ShouldBe([openTask, closedTask]);
    }

    [Test]
    public async Task FeatureBoard_WhenFeatureHasNoStories_ShouldReturnAnEmptyBoard()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await Item("feature", "Empty feature").InsertAsync(connection);

        var view = await ViewAsync(connection, feature);

        view.Sections.Select(s => s.Id).ShouldBe([feature]);
        view.Sections[0].StoriesTotal.ShouldBe(0);
        view.Rows.ShouldBeEmpty();
        view.Header.StoriesTotal.ShouldBe(0);
        view.Header.TasksTotal.ShouldBe(0);
        view.Header.PointsTotal.ShouldBeNull();
        view.Header.AnyInBacklog.ShouldBeFalse();
        view.Header.Sprints.ShouldBeEmpty();
        view.PeopleRows.Single().Person.ShouldBeNull();
        view.PeopleRows.Single().TaskCount.ShouldBe(0);
    }

    [Test]
    public async Task FeatureBoard_WhenRemovedNestedFeatureHasTasksUnderLiftedStories_ShouldShowTheTasks()
    {
        await using var connection = await OpenDatabaseAsync();
        var root = await Item("feature", "Removal tasks root").InsertAsync(connection);
        var removed = await Item("feature", "Removed with tasks").WithParent(root).WithStateNamed("Removed").InsertAsync(connection);
        var orphanStory = await Item("story", "Orphan with task").WithParent(removed).InsertAsync(connection);
        var lifted = await Item("feature", "Lifted with tasks").WithParent(removed).InsertAsync(connection);
        var liftedStory = await Item("story", "Lifted with task").WithParent(lifted).InsertAsync(connection);
        var orphanTask = await Item("task", "Orphan task").WithParent(orphanStory).InsertAsync(connection);
        var liftedTask = await Item("task", "Lifted task").WithParent(liftedStory).InsertAsync(connection);

        var view = await ViewAsync(connection, root);

        view.Rows.Single(r => r.Parent.Id == orphanStory).FeatureId.ShouldBe(root);
        view.Rows.Single(r => r.Parent.Id == liftedStory).FeatureId.ShouldBe(lifted);
        view.Rows.Single(r => r.Parent.Id == orphanStory).Cells.SelectMany(c => c.Tasks).Select(t => t.Id).ShouldBe([orphanTask]);
        view.Rows.Single(r => r.Parent.Id == liftedStory).Cells.SelectMany(c => c.Tasks).Select(t => t.Id).ShouldBe([liftedTask]);
        view.Header.TasksTotal.ShouldBe(2);
        view.PeopleRows.Sum(p => p.TaskCount).ShouldBe(2);
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
