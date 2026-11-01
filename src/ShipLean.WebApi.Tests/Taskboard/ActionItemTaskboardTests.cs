using System.Net;
using System.Text.Json.Nodes;
using Dapper;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard;

// An action item in a sprint is a Taskboard row of its own, with tasks under it.
[TestFixture]
public class ActionItemTaskboardTests : ApiFixture
{
    [Test]
    public async Task Grid_WhenActionItemInSprint_ShouldHaveRow()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, "Action row sprint", 1200);
        var action = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Sprint action").WithSprint(sprint).InsertAsync(connection);
        var task = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Action task").WithParent(action).InsertAsync(connection);

        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{sprint}/taskboard");

        var row = view.Rows.Single(r => r.Parent?.Id == action);
        row.Parent!.Type.Id.ShouldBe(WorkItemTypeId.Action);
        row.Cells.SelectMany(c => c.Tasks).Select(t => t.Id).ShouldBe([task]);
        view.ParentStates.Single(p => p.Type == WorkItemTypeId.Action).States.Select(s => s.Id).ShouldContain(SeedIds.States.Ship.ActionNew);
    }

    [Test]
    public async Task AddTask_WhenUnderActionItem_ShouldCreate()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, "Action add sprint", 1300);
        var action = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Action to add under").WithSprint(sprint).InsertAsync(connection);

        using var response = await Api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = "task",
            ["title"] = "Task under an action item",
            ["parentId"] = action,
            ["sprintId"] = null,
            ["changes"] = null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{sprint}/taskboard");
        view.Rows.Single(r => r.Parent?.Id == action).Cells.SelectMany(c => c.Tasks).Select(t => t.Title).ShouldBe(["Task under an action item"]);
    }

    [Test]
    public async Task Move_WhenTaskUnderActionItem_ShouldMoveIt()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await TaskboardArrangement.InsertSprintAsync(connection, SeedIds.Projects.Ship, "Action move sprint", 1400);
        var action = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Action with a moving task").WithSprint(sprint).InsertAsync(connection);
        var task = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Moving task").WithParent(action).InsertAsync(connection);

        using var response = await Api.PostWireAsync(
            $"api/sprints/{sprint}/taskboard/moves", new ShipLean.Contracts.Taskboard.TaskboardMoveRequest(task, 1, SeedIds.States.Ship.TaskActive, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await connection.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @task", new { task })).ShouldBe(SeedIds.States.Ship.TaskActive);
    }
}
