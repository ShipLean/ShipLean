using System.Net;
using Dapper;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

// GET api/retros/actions/{workItemId}: what the action item's "Why this exists" section reads.
public sealed class ActionDetailTests : RetroPeopleFixture
{
    [Test]
    public async Task ActionDetail_WhenLegacyAction_ShouldShowItsPoint()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Back-copied."));
        var itemId = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(itemId, PointId: pointId));

        var detail = await GetAsync(itemId);

        detail.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("id").GetGuid()).ShouldBe([pointId]);
        detail.GetProperty("action").GetProperty("fixType").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        detail.GetProperty("theme").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        detail.GetProperty("action").GetProperty("pointCount").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task ActionDetail_WhenPointsFromTwoRetros_ShouldReturnBoth()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var first = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "One."));
        var second = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.WentBadly, "Two."));
        var itemId = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(itemId, RaisedSprintId: world.Sprint, FixType: RetroFixType.Guide, PointIds: [first, second]));

        var detail = await GetAsync(itemId);

        detail.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("id").GetGuid()).ShouldBe([first, second]);
        detail.GetProperty("action").GetProperty("fixType").GetString().ShouldBe("guide");
        detail.GetProperty("raisedIn").GetProperty("id").GetGuid().ShouldBe(world.Sprint);
    }

    [Test]
    public async Task ActionDetail_ShouldLeaveOutArchivedLinks()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var kept = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Kept."));
        var dropped = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Taken over."));
        var itemId = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(itemId, PointIds: [kept, dropped]));
        await world.Connection.ExecuteAsync("UPDATE retro_action_point SET archived_at = now() WHERE work_item_id = @itemId AND point_id = @dropped", new { itemId, dropped });

        var detail = await GetAsync(itemId);

        detail.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("id").GetGuid()).ShouldBe([kept]);
        detail.GetProperty("action").GetProperty("pointCount").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task ActionDetail_WhenSprintLevelPoint_ShouldCarryItsSprintAndNoWorkItem()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "About the sprint."));
        var itemId = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(itemId, PointId: pointId));

        var point = (await GetAsync(itemId)).GetProperty("points")[0];

        point.GetProperty("workItem").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        point.GetProperty("sprint").GetProperty("id").GetGuid().ShouldBe(world.Sprint);
    }

    private async Task<System.Text.Json.JsonElement> GetAsync(Guid itemId)
    {
        using var response = await Api.GetAsync($"/api/retros/actions/{itemId}");
        return await ReadAsync(response, HttpStatusCode.OK);
    }
}
