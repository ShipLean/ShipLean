using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Boards.View;

// Drops that need the board arranged first (a second criteria lane) or that change what other tests read; the class has a database of its own.
[TestFixture]
public class BoardMoveLanesApiTests : ApiFixture
{
    private async Task<WriteResult> MoveAsync(Guid boardId, Guid itemId, string column, string? lane = null)
    {
        using var response = await Api.PostJsonAsync($"api/boards/{boardId}/moves", new { itemId, expectedRevision = 1, columnId = column, done = false, laneId = lane });
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<WriteResult>(text, PersistedJson.Options)!;
    }

    private async Task<T> ScalarAsync<T>(string sql, object args)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<T>(sql, args) ?? throw new InvalidOperationException("No row.");
    }

    private async Task AddSprintLaneAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var lane = $$$"""[{"id":"lane-s25","name":"Sprint 25","colour":null,"kind":"criteria","criteria":{"match":"all","clauses":[{"field":"sprint","op":"=","value":"{{{SeedIds.Sprints.Sprint25}}}"}]}}]""";
        await connection.ExecuteAsync(
            "UPDATE board SET settings = jsonb_set(settings, '{swimlanes}', @lane::jsonb || (settings->'swimlanes')) WHERE id = @id",
            new { lane, id = SeedIds.Boards.ShipStories });
    }

    [Test]
    public async Task BoardMove_WhenBetweenCriteriaLanes_ShouldResetTheOldLaneAndWriteTheNew()
    {
        await AddSprintLaneAsync();
        var before = await Api.BoardAsync(SeedIds.Boards.ShipStories);
        before.Lane("lane-s25").Cells.SelectMany(c => c.Cards).Select(c => c.Key).ShouldContain("SHIP-147");

        var result = await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship147, "col-ready", "lane-expedite");

        result.Revision.ShouldBe(2);
        result.Changes.Select(c => c.Field).ShouldContain("sprint");
        (await ScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE id = @id AND sprint_id IS NULL AND priority::int = 1", new { id = SeedIds.WorkItems.Ship147 })).ShouldBe(1);
        var after = await Api.BoardAsync(SeedIds.Boards.ShipStories);
        after.Lane("lane-expedite").Cells.SelectMany(c => c.Cards).Select(c => c.Key).ShouldContain("SHIP-147");
        after.Lane("lane-s25").Cells.SelectMany(c => c.Cards).Select(c => c.Key).ShouldNotContain("SHIP-147");
    }

    [Test]
    public async Task BoardMove_WhenIntoCriteriaLaneWithNoClauses_ShouldRefuseAndNotOfferDrops()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            const string lane = """[{"id":"lane-blank","name":"Blank","colour":null,"kind":"criteria","criteria":{"match":"all","clauses":[]}}]""";
            await connection.ExecuteAsync(
                "UPDATE board SET settings = jsonb_set(settings, '{swimlanes}', @lane::jsonb || (settings->'swimlanes')) WHERE id = @id",
                new { lane, id = SeedIds.Boards.ShipStories });
        }

        var board = await Api.BoardAsync(SeedIds.Boards.ShipStories);
        board.Lane("lane-blank").AcceptsDrops.ShouldBeFalse();
        board.Lane("lane-blank").Cells.SelectMany(c => c.Cards).ShouldBeEmpty();

        var revision = await ScalarAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship147 });

        using var response = await Api.PostJsonAsync($"api/boards/{SeedIds.Boards.ShipStories}/moves", new { itemId = SeedIds.WorkItems.Ship147, expectedRevision = revision, columnId = "col-ready", done = false, laneId = "lane-blank" });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("This lane has no criteria");
    }

    [Test]
    public async Task BoardMove_WhenIntoCriteriaLaneWithNullCriteria_ShouldRefuseAndNotOfferDrops()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            const string lane = """[{"id":"lane-null","name":"Unset","colour":null,"kind":"criteria","criteria":null}]""";
            await connection.ExecuteAsync(
                "UPDATE board SET settings = jsonb_set(settings, '{swimlanes}', @lane::jsonb || (settings->'swimlanes')) WHERE id = @id",
                new { lane, id = SeedIds.Boards.ShipStories });
        }

        var board = await Api.BoardAsync(SeedIds.Boards.ShipStories);
        board.Lane("lane-null").AcceptsDrops.ShouldBeFalse();
        board.Lane("lane-null").Cells.SelectMany(c => c.Cards).ShouldBeEmpty();

        var revision = await ScalarAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship147 });

        using var response = await Api.PostJsonAsync($"api/boards/{SeedIds.Boards.ShipStories}/moves", new { itemId = SeedIds.WorkItems.Ship147, expectedRevision = revision, columnId = "col-ready", done = false, laneId = "lane-null" });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("This lane has no criteria");
    }

    [Test]
    public async Task BoardMove_WhenNestedFeatureDroppedInAnotherEpicLane_ShouldBecomeItsDirectChildInOneRevision()
    {
        var result = await MoveAsync(SeedIds.Boards.ShipFeatures, SeedIds.WorkItems.Ship108, "col-shaping", $"epic-{SeedIds.WorkItems.Ship90}");

        result.Revision.ShouldBe(2);
        result.Changes.Select(c => c.Field).ShouldBe(["parent"]);
        (await ScalarAsync<Guid>("SELECT parent_id FROM work_item WHERE id = @id", new { id = SeedIds.WorkItems.Ship108 })).ShouldBe(SeedIds.WorkItems.Ship90);
        (await ScalarAsync<int>("SELECT count(*)::int FROM revision WHERE work_item_id = @id", new { id = SeedIds.WorkItems.Ship108 })).ShouldBe(2);
    }

    [Test]
    public async Task Board_WhenCardEntersDone_ShouldKeepTheOlderCursorOfTheCell()
    {
        var done = (await Api.BoardAsync(SeedIds.Boards.ShipStories)).Lane("lane-default").Cell("col-done");
        var cursor = done.OlderCursor.ShouldNotBeNull();

        await MoveAsync(SeedIds.Boards.ShipStories, SeedIds.WorkItems.Ship166, "col-done");

        var cell = (await Api.BoardAsync(SeedIds.Boards.ShipStories, $"?older={Uri.EscapeDataString(cursor)}")).Lane("lane-default").Cell("col-done");
        cell.OlderCount.ShouldBe(0);
        cell.Cards.Select(c => c.Key).ShouldContain("SHIP-166");
        cell.Cards.Count.ShouldBeGreaterThan(4);
    }
}
