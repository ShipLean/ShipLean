using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Taskboard;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard;

[TestFixture]
public class TaskboardMoveApiTests : ApiFixture
{
    [Test]
    public async Task TaskMove_WhenRowIsPerson_ShouldSetAssignee()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Person move");

        var result = await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskActive, new(TaskboardGrouping.People, SeedIds.Users.Priya), null));

        result.Written.Revision.ShouldBe(2);
        (await connection.ExecuteScalarAsync<Guid?>("SELECT assigned_to FROM work_item WHERE id = @id", new { id = board.StoryOpenOne })).ShouldBe(SeedIds.Users.Priya);
        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");
        view.PeopleRows.Single(r => r.Person?.Id == SeedIds.Users.Priya).Cells.Single(c => c.StateId == SeedIds.States.Ship.TaskActive).Tasks.Single().Id.ShouldBe(board.StoryOpenOne);

        var cleared = await MoveAsync(board.SprintId, new(board.StoryOpenOne, 2, SeedIds.States.Ship.TaskActive, new(TaskboardGrouping.People, null), null));
        cleared.Written.Revision.ShouldBe(3);
        (await connection.ExecuteScalarAsync<Guid?>("SELECT assigned_to FROM work_item WHERE id = @id", new { id = board.StoryOpenOne })).ShouldBeNull();
    }

    [Test]
    public async Task TaskMove_WhenStale_ShouldBe409()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Stale move");
        await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskActive, null, null));

        using var response = await Api.PostWireAsync($"api/sprints/{board.SprintId}/taskboard/moves", new TaskboardMoveRequest(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskClosed, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var conflict = System.Text.Json.JsonSerializer.Deserialize<ConflictDto>(await response.Content.ReadAsStringAsync(), TaskboardWire.Options)!;
        conflict.Code.ShouldBe(ApiErrorCodes.Conflict);
        conflict.Current.Revision.ShouldBe(2);
        (await connection.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id = board.StoryOpenOne })).ShouldBe(SeedIds.States.Ship.TaskActive);
    }

    [Test]
    public async Task Taskboard_WhenLastTaskClosed_ShouldFlagRow()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Resolve flag");

        var first = await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskClosed, null, null));
        first.OfferResolve.ShouldBeFalse();
        (await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard")).Rows[0].OfferResolve.ShouldBeFalse();

        var last = await MoveAsync(board.SprintId, new(board.StoryOpenTwo, 1, SeedIds.States.Ship.TaskClosed, null, null));

        last.OfferResolve.ShouldBeTrue();
        last.ParentId.ShouldBe(board.Story);
        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");
        view.Rows.Select(r => r.OfferResolve).ShouldBe([true, false, false]);
    }

    [Test]
    public async Task TaskMove_WhenDraggedToOtherStoryRow_ShouldReparentInOneRevision()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Reparent move");

        var result = await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskActive, new(TaskboardGrouping.Stories, board.Bug), null));

        result.Written.Revision.ShouldBe(2);
        result.ParentId.ShouldBe(board.Bug);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM revision WHERE work_item_id = @id", new { id = board.StoryOpenOne })).ShouldBe(2);
        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");
        view.Rows.Single(r => r.Parent?.Id == board.Bug).Cells.SelectMany(c => c.Tasks).Select(t => t.Id).ShouldBe([board.BugTask, board.StoryOpenOne], ignoreOrder: true);
    }

    [Test]
    public async Task TaskMove_WhenDroppedAboveCard_ShouldPersistCellOrder()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Order move");

        await MoveAsync(board.SprintId, new(board.StoryOpenTwo, 1, SeedIds.States.Ship.TaskNew, null, board.StoryOpenOne));

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");
        view.Rows[0].Cells[0].Tasks.Select(t => t.Id).ShouldBe([board.StoryOpenTwo, board.StoryOpenOne]);
    }

    [Test]
    public async Task TaskMove_WhenDroppedLastInACellOfUnplacedCards_ShouldListItAfterThem()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Last move");

        await MoveAsync(board.SprintId, new(board.BugTask, 1, SeedIds.States.Ship.TaskNew, new(TaskboardGrouping.Stories, board.Story), null));

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");
        view.Rows[0].Cells[0].Tasks.Select(t => t.Id).ShouldBe([board.StoryOpenOne, board.StoryOpenTwo, board.BugTask]);
    }

    [Test]
    public async Task TaskMove_WhenDroppedOnAPeopleRow_ShouldIgnoreBeforeItemAndPlaceTheTaskLastInItsCell()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "People move");
        var inReview = SeedIds.States.Ship.TaskInReview; // not In Progress: that column would assign the mover
        var unassigned = new TaskboardMoveRow(TaskboardGrouping.People, null);
        await MoveAsync(board.SprintId, new(board.StoryOpenTwo, 1, inReview, unassigned, null));

        // A card of another parent: the Stories rule would 404 on it. A card of the same parent: the Stories rule would put the task above it.
        await MoveAsync(board.SprintId, new(board.BugTask, 1, inReview, unassigned, board.StoryOpenTwo));
        await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, inReview, unassigned, board.StoryOpenTwo));

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");
        view.Rows[0].Cells.Single(c => c.StateId == inReview).Tasks.Select(t => t.Id).ShouldBe([board.StoryOpenTwo, board.StoryOpenOne]);
        view.PeopleRows[0].Cells.Single(c => c.StateId == inReview).Tasks.Select(t => t.Id).ShouldBe([board.StoryOpenTwo, board.StoryOpenOne, board.BugTask]);
    }

    [Test]
    public async Task TaskMove_WhenTaskIsNotOnThisSprintsBoard_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Off-board move");

        using var response = await Api.PostWireAsync($"api/sprints/{board.SprintId}/taskboard/moves",
            new TaskboardMoveRequest(board.OtherSprintTask, 1, SeedIds.States.Ship.TaskActive, null, null));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await connection.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id = board.OtherSprintTask })).ShouldBe(SeedIds.States.Ship.TaskNew);
    }

    [Test]
    public async Task TaskMove_WhenParentHasReachedResolved_ShouldNotOfferResolve()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Resolved parent move");
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @resolved WHERE id = @id", new { resolved = SeedIds.States.Ship.StoryResolved, id = board.Story });
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @closed WHERE id = @id", new { closed = SeedIds.States.Ship.TaskClosed, id = board.StoryOpenTwo });

        var result = await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskClosed, null, null));

        result.OfferResolve.ShouldBeFalse();
        (await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard")).Rows[0].OfferResolve.ShouldBeFalse();
    }

    [Test]
    public async Task TaskMove_WhenAClosedTaskIsReparentedOntoAClosedRow_ShouldOfferResolve()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Closed reparent move");
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @closed WHERE id = ANY(@ids)",
            new { closed = SeedIds.States.Ship.TaskClosed, ids = new[] { board.StoryOpenOne, board.BugTask } });

        var result = await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskClosed, new(TaskboardGrouping.Stories, board.Bug), null));

        result.ParentId.ShouldBe(board.Bug);
        result.OfferResolve.ShouldBeTrue();
    }

    [Test]
    public async Task TaskMove_WhenDroppedOnUnparentedRow_ShouldClearParent()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Orphan move");

        await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskNew, new(TaskboardGrouping.Stories, null), null));

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");
        view.Rows[^1].Cells.SelectMany(c => c.Tasks).Select(t => t.Id).ShouldBe([board.Unparented, board.StoryOpenOne], ignoreOrder: true);
    }

    [Test]
    public async Task TaskMove_WhenStoryIsOutsideTheSprint_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Outside move");

        using var response = await Api.PostWireAsync($"api/sprints/{board.SprintId}/taskboard/moves",
            new TaskboardMoveRequest(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskNew, new(TaskboardGrouping.Stories, board.OtherSprintStory), null));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await connection.ExecuteScalarAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id = board.StoryOpenOne })).ShouldBe(1);
    }

    [Test]
    public async Task TaskMove_WhenStateIsNotATaskState_ShouldBeValidation()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Wrong state move");

        using var response = await Api.PostWireAsync($"api/sprints/{board.SprintId}/taskboard/moves",
            new TaskboardMoveRequest(board.StoryOpenOne, 1, SeedIds.States.Ship.StoryActive, null, null));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task TaskMove_WhenNotMember_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Part, "Not my moves", 60);

        using var response = await Api.PostWireAsync($"api/sprints/{sprint}/taskboard/moves",
            new TaskboardMoveRequest(Guid.NewGuid(), 1, SeedIds.States.Ship.TaskNew, null, null));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task TaskMove_WhenDropped_ShouldPublishWorkItemMoved()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Event move");
        using var watch = new TaskboardEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);

        await MoveAsync(board.SprintId, new(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskActive, new(TaskboardGrouping.Stories, board.Bug), null));

        var moved = await watch.NextAsync<WorkItemMoved>(e => e.Id == board.StoryOpenOne);
        moved.StateId.ShouldBe(SeedIds.States.Ship.TaskActive);
        moved.ParentId.ShouldBe(board.Bug);
        moved.Revision.ShouldBe(2);
    }

    private async Task<TaskboardMoveResultDto> MoveAsync(Guid sprintId, TaskboardMoveRequest request)
    {
        using var response = await Api.PostWireAsync($"api/sprints/{sprintId}/taskboard/moves", request);
        return await response.ReadAsync<TaskboardMoveResultDto>();
    }
}
