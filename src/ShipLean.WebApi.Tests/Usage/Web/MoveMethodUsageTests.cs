using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Taskboard;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Usage.Web;

// K3: the method a move request sends goes on its work_item.moved event; none sends none; an unknown one moves nothing.
[TestFixture]
public class MoveMethodUsageTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    private UsageEvent Only(string source) => Recorder.Events.Where(e => e.Source == source).ShouldHaveSingleItem();

    private async Task<Guid> InsertBoardCardAsync(string title)
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle(title).WithParent(SeedIds.WorkItems.Ship104).WithBoardPosition("col-new").InsertAsync(connection);
    }

    [Test]
    public async Task BoardMove_WhenMethodIsTouchDrag_ShouldTrackMethod()
    {
        var id = await InsertBoardCardAsync("Touch dragged action");

        using var response = await Api.PostJsonAsync($"api/boards/{SeedIds.Boards.ShipStories}/moves", new { itemId = id, expectedRevision = 1, columnId = "col-done", done = false, laneId = (string?)null, method = MoveMethods.TouchDrag });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tracked = Only("api:BoardView.Move");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemMoved);
        tracked.Properties[UsageProps.Method].ShouldBe(MoveMethods.TouchDrag);
        tracked.Properties[UsageProps.Board].ShouldBe("board");
    }

    [Test]
    public async Task BoardMove_WhenMethodIsUnknown_ShouldBeValidationAndNotMove()
    {
        var id = await InsertBoardCardAsync("Unknown method action");

        using var response = await Api.PostJsonAsync($"api/boards/{SeedIds.Boards.ShipStories}/moves", new { itemId = id, expectedRevision = 1, columnId = "col-done", done = false, laneId = (string?)null, method = "teleport" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("method");
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id })).ShouldBe(1);
    }

    [Test]
    public async Task FeatureBoardMove_WhenMethodIsTouchDrag_ShouldTrackMethod()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Method feature move");

        using var response = await Api.PostWireAsync($"api/work-items/{board.Feature}/feature-board/moves",
            new TaskboardMoveRequest(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskClosed, null, null, MoveMethods.TouchDrag));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tracked = Only("api:FeatureBoard.Move");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemMoved);
        tracked.Properties[UsageProps.Method].ShouldBe(MoveMethods.TouchDrag);
    }

    [Test]
    public async Task TaskboardMove_WhenMethodIsSheet_ShouldTrackMethod()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Method sheet move");

        using var response = await Api.PostWireAsync($"api/sprints/{board.SprintId}/taskboard/moves",
            new TaskboardMoveRequest(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskActive, null, null, MoveMethods.Sheet));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tracked = Only("api:Taskboard.Move");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemMoved);
        tracked.Properties[UsageProps.Method].ShouldBe(MoveMethods.Sheet);
    }

    [Test]
    public async Task TaskboardMove_WhenNoMethod_ShouldTrackNoMethod()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Method none move");

        using var response = await Api.PostWireAsync($"api/sprints/{board.SprintId}/taskboard/moves",
            new TaskboardMoveRequest(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskActive, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tracked = Only("api:Taskboard.Move");
        tracked.Name.ShouldBe(UsageEventNames.WorkItemMoved);
        tracked.Properties.ShouldNotContainKey(UsageProps.Method);
    }

    [Test]
    public async Task TaskboardMove_WhenMethodIsUnknown_ShouldBeValidationAndNotMove()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, "Method unknown move");

        using var response = await Api.PostWireAsync($"api/sprints/{board.SprintId}/taskboard/moves",
            new TaskboardMoveRequest(board.StoryOpenOne, 1, SeedIds.States.Ship.TaskActive, null, null, "Sheet"));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("method");
        (await connection.ExecuteScalarAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id = board.StoryOpenOne })).ShouldBe(1);
    }
}
