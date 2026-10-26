using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard;

[TestFixture]
public class TaskboardReadApiTests : ApiFixture
{
    [Test]
    public async Task Taskboard_WhenLoaded_ShouldHaveUnparentedRowLast()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Read sprint");

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");

        view.SprintId.ShouldBe(board.SprintId);
        view.Rows.Select(r => r.Parent?.Id).ShouldBe([board.Story, board.Bug, null]);
        view.Rows[^1].Cells.SelectMany(c => c.Tasks).Select(t => t.Id).ShouldBe([board.Unparented]);
        view.Rows[0].Cells.SelectMany(c => c.Tasks).Select(t => t.Id).ShouldBe([board.StoryOpenOne, board.StoryOpenTwo]);
        view.Rows[0].Cells.Select(c => c.StateId).ShouldBe(view.Columns.Select(c => c.State.Id));
    }

    [Test]
    public async Task Taskboard_WhenLoaded_ShouldListTaskStatesWithoutRemovedAsColumns()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Columns sprint");

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");

        view.Columns.Select(c => c.State.Id).ShouldBe([SeedIds.States.Ship.TaskNew, SeedIds.States.Ship.TaskActive, SeedIds.States.Ship.TaskInReview, SeedIds.States.Ship.TaskClosed]);
        view.Columns[0].Count.ShouldBe(4);
        view.ParentStates.Select(p => p.Type).ShouldBe([WorkItemTypeId.Story, WorkItemTypeId.Bug]);
        view.ParentStates.SelectMany(p => p.States).ShouldAllBe(s => s.Category != StateCategory.Removed);
        view.BoardId.ShouldBe(SeedIds.Boards.ShipTaskboard);
    }

    [Test]
    public async Task Taskboard_WhenStoryIsInAnotherSprint_ShouldNotBeARow()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Scoped sprint");

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");

        view.Rows.Select(r => r.Parent?.Id).ShouldNotContain(board.OtherSprintStory);
        view.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Tasks).Count().ShouldBe(4);
    }

    [Test]
    public async Task Taskboard_WhenGroupedByPeople_ShouldPutUnassignedFirst()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "People sprint");
        await connection.ExecuteAsync("UPDATE work_item SET assigned_to = @user WHERE id = @id", new { user = SeedIds.Users.Priya, id = board.StoryOpenTwo });

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");

        view.PeopleRows.Select(r => r.Person?.Id).ShouldBe([null, SeedIds.Users.Priya]);
        view.PeopleRows.Select(r => r.TaskCount).ShouldBe([3, 1]);
        view.PeopleRows[1].Cells.SelectMany(c => c.Tasks).Single().Parent!.Id.ShouldBe(board.Story);
    }

    [Test]
    public async Task Taskboard_WhenTaskIsBlocked_ShouldColourTagAndPickStyleRule()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Style sprint");
        await connection.ExecuteAsync("UPDATE work_item SET tags = ARRAY['Blocked'] WHERE id = @id", new { id = board.BugTask });

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");

        var cards = view.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Tasks).ToDictionary(t => t.Id);
        cards[board.BugTask].Tags.ShouldBe([new CardTagDto("Blocked", Swatch.Danger)]);
        cards[board.BugTask].StyleRuleId.ShouldBe("rule-blocked");
        cards[board.StoryOpenOne].StyleRuleId.ShouldBeNull();
        view.Rows[0].Parent!.Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Task, 2, 0)]);
    }

    [Test]
    public async Task Taskboard_WhenNotMember_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Part, "Not my sprint", 50);

        using var response = await Api.GetAsync($"api/sprints/{sprint}/taskboard");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
