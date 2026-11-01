using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Boards.View;

// Action items on the SHIP boards: the seeded SHIP-89 (under Feature SHIP-104, state New, no sprint) and items the tests make.
[TestFixture]
public class ActionBoardTests : ApiFixture
{
    [Test]
    public async Task StoriesBoard_WhenAction_ShouldShowInStateColumn()
    {
        using var created = await Api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = "action",
            ["title"] = "Action created on the board",
            ["parentId"] = SeedIds.WorkItems.Ship104,
        });
        var text = await created.Content.ReadAsStringAsync();
        created.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var key = JsonNode.Parse(text)!["key"]!.GetValue<string>();

        var board = await Api.BoardAsync(SeedIds.Boards.ShipStories);

        var card = board.Cards().Single(c => c.Key == key);
        card.Type.Id.ShouldBe(WorkItemTypeId.Action);
        card.State.Id.ShouldBe(SeedIds.States.Ship.ActionNew);
        board.Lane("lane-default").Cell("col-new").Cards.Select(c => c.Key).ShouldContain(key);
    }

    [Test]
    public async Task StoriesBoard_WhenMovedToClosedColumn_ShouldCloseIt()
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Action to close").WithParent(SeedIds.WorkItems.Ship104).WithBoardPosition("col-new").InsertAsync(connection);

        using var response = await Api.PostJsonAsync($"api/boards/{SeedIds.Boards.ShipStories}/moves", new { itemId = id, expectedRevision = 1, columnId = "col-done", done = false, laneId = (string?)null });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonSerializer.Deserialize<WriteResult>(text, PersistedJson.Options)!.Changes.Select(c => c.Field).ShouldContain("state");
        (await connection.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id })).ShouldBe(SeedIds.States.Ship.ActionClosed);
    }

    [Test]
    public async Task FeaturesBoard_WhenActionUnderFeature_ShouldRollUpItsPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Feature with an action item").WithBoardPosition("col-new").InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Story under it").WithParent(feature).WithPoints(3).InsertAsync(connection);
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Action under it").WithParent(feature).WithPoints(5).InsertAsync(connection);
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });

        var board = await Api.BoardAsync(SeedIds.Boards.ShipFeatures);

        var card = board.Cards().Single(c => c.Key == key);
        card.ChildPoints.ShouldBe(8m);
        card.Rollups.Select(r => r.Type).ShouldBe([WorkItemTypeId.Story, WorkItemTypeId.Action]);
    }
}
