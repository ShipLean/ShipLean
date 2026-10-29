using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Taskboard;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard;

[TestFixture]
public class FeatureBoardMoveApiTests : ApiFixture
{
    [Test]
    public async Task FeatureBoardMove_WhenDroppedInAColumn_ShouldWriteTheState()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Column move");

        var result = await MoveAsync(board.Feature, new(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskClosed, null, null));

        result.Written.Revision.ShouldBe(2);
        result.ParentId.ShouldBe(board.Story);
        result.OfferResolve.ShouldBeFalse();
        (await connection.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id = board.StoryTaskOne })).ShouldBe(SeedIds.States.Ship.TaskClosed);
    }

    [Test]
    public async Task FeatureBoardMove_WhenReparentedToAStoryInANestedFeature_ShouldLeaveTheSprintAlone()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Reparent move");
        var sprintBefore = await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @id", new { id = board.StoryTaskOne });

        var result = await MoveAsync(board.Feature, new(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskNew, new(TaskboardGrouping.Stories, board.NestedStory), null));

        result.ParentId.ShouldBe(board.NestedStory);
        (await connection.ExecuteScalarAsync<Guid?>("SELECT parent_id FROM work_item WHERE id = @id", new { id = board.StoryTaskOne })).ShouldBe(board.NestedStory);
        (await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @id", new { id = board.StoryTaskOne })).ShouldBe(sprintBefore);
        sprintBefore.ShouldBeNull();
    }

    [Test]
    public async Task FeatureBoardMove_WhenDroppedOnAPersonRow_ShouldSetTheAssignee()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Person move");

        await MoveAsync(board.Feature, new(board.NestedTask, 1, SeedIds.States.Ship.TaskInReview, new(TaskboardGrouping.People, SeedIds.Users.Priya), null));

        (await connection.ExecuteScalarAsync<Guid?>("SELECT assigned_to FROM work_item WHERE id = @id", new { id = board.NestedTask })).ShouldBe(SeedIds.Users.Priya);
        (await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @id", new { id = board.NestedTask })).ShouldBeNull();
    }

    [Test]
    public async Task FeatureBoardMove_WhenTheStoryIsOutsideTheTree_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Outside story move");

        using var toOutside = await Api.PostWireAsync($"api/work-items/{board.Feature}/feature-board/moves",
            new TaskboardMoveRequest(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskNew, new(TaskboardGrouping.Stories, board.OutsideStory), null));
        using var fromOutside = await Api.PostWireAsync($"api/work-items/{board.Feature}/feature-board/moves",
            new TaskboardMoveRequest(board.OutsideTask, 1, SeedIds.States.Ship.TaskActive, null, null));

        (await ApiClient.ReadApiErrorAsync(toOutside, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await ApiClient.ReadApiErrorAsync(fromOutside, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await connection.ExecuteScalarAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id = board.StoryTaskOne })).ShouldBe(1);
    }

    [Test]
    public async Task FeatureBoardMove_WhenTheStoriesTargetIsNull_ShouldBeValidation()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Unparented move");

        using var response = await Api.PostWireAsync($"api/work-items/{board.Feature}/feature-board/moves",
            new TaskboardMoveRequest(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskNew, new(TaskboardGrouping.Stories, null), null));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await connection.ExecuteScalarAsync<Guid?>("SELECT parent_id FROM work_item WHERE id = @id", new { id = board.StoryTaskOne })).ShouldBe(board.Story);
    }

    [Test]
    public async Task FeatureBoardMove_WhenTheFeatureIsRemoved_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Removed board move");
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @removed WHERE id = @id", new { removed = SeedIds.States.Ship.FeatureRemoved, id = board.Feature });

        using var response = await Api.PostWireAsync($"api/work-items/{board.Feature}/feature-board/moves",
            new TaskboardMoveRequest(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskActive, null, null));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task FeatureBoardMove_WhenTheRevisionIsStale_ShouldBe409()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Stale move");
        await MoveAsync(board.Feature, new(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskActive, null, null));

        using var response = await Api.PostWireAsync($"api/work-items/{board.Feature}/feature-board/moves",
            new TaskboardMoveRequest(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskClosed, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var conflict = System.Text.Json.JsonSerializer.Deserialize<ConflictDto>(await response.Content.ReadAsStringAsync(), TaskboardWire.Options)!;
        conflict.Current.Revision.ShouldBe(2);
        (await connection.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id = board.StoryTaskOne })).ShouldBe(SeedIds.States.Ship.TaskActive);
    }

    [Test]
    public async Task FeatureBoardMove_WhenTheLastOpenTaskCloses_ShouldOfferResolve()
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await FeatureBoardMoveArrangement.InsertAsync(connection, "Resolve offer");

        var first = await MoveAsync(board.Feature, new(board.StoryTaskOne, 1, SeedIds.States.Ship.TaskClosed, null, null));
        var last = await MoveAsync(board.Feature, new(board.StoryTaskTwo, 1, SeedIds.States.Ship.TaskClosed, null, null));

        first.OfferResolve.ShouldBeFalse();
        last.OfferResolve.ShouldBeTrue();
        last.ParentId.ShouldBe(board.Story);
    }

    private async Task<TaskboardMoveResultDto> MoveAsync(Guid featureId, TaskboardMoveRequest request)
    {
        using var response = await Api.PostWireAsync($"api/work-items/{featureId}/feature-board/moves", request);
        return await response.ReadAsync<TaskboardMoveResultDto>();
    }
}
