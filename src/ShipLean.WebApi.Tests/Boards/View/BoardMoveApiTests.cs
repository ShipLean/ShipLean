using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Boards.View;

// Cards dropped on the SHIP boards. Each test moves cards no other test of the class moves.
[TestFixture]
public class BoardMoveApiTests : ApiFixture
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private Task<HttpResponseMessage> MoveAsync(Guid boardId, Guid itemId, int revision, string column, bool done = false, string? lane = null)
        => Api.PostJsonAsync($"api/boards/{boardId}/moves", new { itemId, expectedRevision = revision, columnId = column, done, laneId = lane });

    private static async Task<WriteResult> WrittenAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<WriteResult>(text, PersistedJson.Options)!;
    }

    private async Task<T> ScalarAsync<T>(string sql, object args)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<T>(sql, args) ?? throw new InvalidOperationException("No row.");
    }

    [Test]
    public async Task BoardMove_WhenIntoDoneHalf_ShouldKeepState()
    {
        var before = await ScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship147 });

        var result = await WrittenAsync(await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship147, 1, "col-ready", done: true));

        result.Changes.ShouldBeEmpty();
        result.Revision.ShouldBe(1);
        (await ScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship147 })).ShouldBe(before);
        var board = await Api.BoardAsync(SeedIds.Boards.ShipStories);
        board.Lane("lane-default").Cell("col-ready", doneHalf: true).Cards.Select(c => c.Key).ShouldContain("SHIP-147");
        board.Lane("lane-default").Cell("col-ready").Cards.Select(c => c.Key).ShouldNotContain("SHIP-147");
    }

    [Test]
    public async Task BoardMove_WhenIntoEpicLane_ShouldReparentInOneRevision()
    {
        var result = await WrittenAsync(await MoveAsync(SeedIds.Boards.ShipFeatures, SeedIds.WorkItems.Ship102, 1, "col-in-progress", lane: $"epic-{SeedIds.WorkItems.Ship90}"));

        result.Revision.ShouldBe(2);
        result.Changes.Select(c => c.Field).ShouldBe(["parent"]);
        (await ScalarAsync<Guid>("SELECT parent_id FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship102 })).ShouldBe(SeedIds.WorkItems.Ship90);
        (await ScalarAsync<int>("SELECT count(*)::int FROM revision WHERE work_item_id = @id", new { id = SeedIds.WorkItems.Ship102 })).ShouldBe(2);
        (await ScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE id = @id AND swimlane_id IS NULL", new { id = SeedIds.WorkItems.Ship102 })).ShouldBe(1);
        await Api.SetLanePreferenceAsync(SeedIds.Boards.ShipFeatures, "epic");
        (await Api.BoardAsync(SeedIds.Boards.ShipFeatures)).Lane($"epic-{SeedIds.WorkItems.Ship90}").Cell("col-in-progress").Cards.Select(c => c.Key).ShouldContain("SHIP-102");
    }

    [Test]
    public async Task BoardMove_WhenIntoNoEpicLane_ShouldClearTheParent()
    {
        var result = await WrittenAsync(await MoveAsync(SeedIds.Boards.ShipFeatures, SeedIds.WorkItems.Ship104, 1, "col-new", lane: "lane-no-epic"));

        result.Changes.Select(c => c.Field).ShouldBe(["parent"]);
        (await ScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE id = @id AND parent_id IS NULL", new { id = SeedIds.WorkItems.Ship104 })).ShouldBe(1);
    }

    [Test]
    public async Task BoardMove_WhenNestedFeatureDroppedInItsOwnEpicLane_ShouldKeepItsFeatureParent()
    {
        var result = await WrittenAsync(await MoveAsync(SeedIds.Boards.ShipFeatures, SeedIds.WorkItems.Ship108, 1, "col-shaping", lane: $"epic-{SeedIds.WorkItems.Ship92}"));

        result.Changes.ShouldBeEmpty();
        (await ScalarAsync<Guid>("SELECT parent_id FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship108 })).ShouldBe(SeedIds.WorkItems.Ship103);
    }

    [Test]
    public async Task BoardMove_WhenEpicLaneUnknown_ShouldBeValidationError()
    {
        using var response = await MoveAsync(SeedIds.Boards.ShipFeatures, SeedIds.WorkItems.Ship103, 1, "col-new", lane: $"epic-{Guid.NewGuid()}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Move_WhenStale_ShouldBe409()
    {
        using var response = await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship156, 99, "col-ready");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var conflict = JsonSerializer.Deserialize<ConflictDto>(await response.Content.ReadAsStringAsync(), PersistedJson.Options)!;
        conflict.Code.ShouldBe(ApiErrorCodes.Conflict);
        conflict.Current.Key.ShouldBe("SHIP-156");
        conflict.Current.Revision.ShouldBe(1);
        (await ScalarAsync<string>("SELECT board_column_id FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship156 })).ShouldBe("col-new");
    }

    [Test]
    public async Task Move_WhenStaleAndLaneUnknown_ShouldBe409()
    {
        using var response = await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship156, 99, "col-ready", lane: "lane-gone");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        JsonSerializer.Deserialize<ConflictDto>(await response.Content.ReadAsStringAsync(), PersistedJson.Options)!.Current.Key.ShouldBe("SHIP-156");
    }

    [Test]
    public async Task BoardMove_WhenIntoCriteriaLane_ShouldWriteItsClausesInTheSameRevision()
    {
        var result = await WrittenAsync(await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship163, 1, "col-new", lane: "lane-expedite"));

        result.Revision.ShouldBe(2);
        result.Changes.Select(c => c.Field).ShouldBe(["priority"]);
        (await ScalarAsync<int>("SELECT priority::int FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship163 })).ShouldBe(1);
        (await Api.BoardAsync(SeedIds.Boards.ShipStories)).Lane("lane-expedite").Cell("col-new").Cards.Select(c => c.Key).ShouldContain("SHIP-163");
    }

    [Test]
    public async Task BoardMove_WhenOutOfCriteriaLane_ShouldWriteTheDefaultPriority()
    {
        var result = await WrittenAsync(await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship160, 1, "col-ready", done: true, lane: "lane-default"));

        result.Changes.Select(c => c.Field).ShouldBe(["priority"]);
        (await ScalarAsync<int>("SELECT priority::int FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship160 })).ShouldBe(2);
        (await Api.BoardAsync(SeedIds.Boards.ShipStories)).Lane("lane-default").Cell("col-ready", doneHalf: true).Cards.Select(c => c.Key).ShouldContain("SHIP-160");
    }

    [Test]
    public async Task BoardMove_WhenIntoLaneWithOtherOperators_ShouldBeValidationError()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            """
            UPDATE board SET settings = jsonb_set(settings, '{swimlanes}', settings->'swimlanes' || '[{"id":"lane-spike","name":"Spikes","colour":null,"kind":"criteria","criteria":{"match":"all","clauses":[{"field":"tags","op":"contains","value":"Spike"}]}}]'::jsonb)
            WHERE id = @id
            """, new { id = SeedIds.Boards.ShipStories });
        var board = await Api.BoardAsync(SeedIds.Boards.ShipStories);
        board.Lane("lane-spike").AcceptsDrops.ShouldBeFalse();
        board.Lane("lane-spike").CriteriaText.ShouldBe("Tags contains Spike");

        using var response = await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship165, 1, "col-new", lane: "lane-spike");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await ScalarAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship165 })).ShouldBe(1);
    }

    [Test]
    public async Task BoardMove_WhenCardMoved_ShouldPublishWorkItemMoved()
    {
        var published = Channel.CreateUnbounded<WorkItemMoved>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(SeedIds.Projects.Ship, e =>
        {
            if (e is WorkItemMoved moved && moved.Id == SeedIds.WorkItems.Ship166)
            {
                published.Writer.TryWrite(moved);
            }
            return Task.CompletedTask;
        });

        var result = await WrittenAsync(await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship166, 1, "col-in-progress"));

        using var timeout = new CancellationTokenSource(Patience);
        var moved = await published.Reader.ReadAsync(timeout.Token);
        moved.ColumnId.ShouldBe("col-in-progress");
        moved.Done.ShouldBeFalse();
        moved.Revision.ShouldBe(result.Revision);
        moved.StateId.ShouldBe(await ScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship166 }));
        moved.ClientId.ShouldBe(ApiClient.ClientId);
    }

    [Test]
    public async Task BoardMove_WhenNotMember_ShouldBeNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync(
            $"api/boards/{SeedIds.Boards.ShipStories}/moves",
            new { itemId = SeedIds.WorkItems.Ship162, expectedRevision = 1, columnId = "col-new", done = false });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task BoardMove_WhenCardIsAnotherLevel_ShouldBeValidationError()
    {
        using var response = await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship101, 1, "col-new");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task BoardMove_WhenColumnNotOnBoard_ShouldBeValidationError()
    {
        using var response = await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship158, 1, "col-nope");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
