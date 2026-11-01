using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

public sealed class RetroActionCreateTests : RetroPeopleFixture
{
    [Test]
    public async Task Create_WhenImprovePoint_ShouldCreateActionItemLinkedToTheme()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var theme = await world.Seed.ThemeAsync("Slow CI");
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Cache the build.", ThemeId: theme));
        using var watch = Watch(world.Project.Id);

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { title = "Cache the build", points = 3 });

        var action = await ReadAsync(response, HttpStatusCode.Created);
        action.GetProperty("title").GetString().ShouldBe("Cache the build");
        action.GetProperty("state").GetProperty("name").GetString().ShouldBe("New");
        action.GetProperty("points").GetDecimal().ShouldBe(3m);
        var itemId = action.GetProperty("workItemId").GetGuid();
        using var item = await Api.GetAsync($"/api/work-items/{itemId}");
        var dto = await ReadAsync(item, HttpStatusCode.OK);
        dto.GetProperty("type").GetProperty("id").GetString().ShouldBe("action");
        dto.GetProperty("state").GetProperty("name").GetString().ShouldBe("New");
        var link = await world.Connection.QuerySingleAsync<(Guid Theme, Guid Point, Guid Sprint)>("SELECT theme_id AS Theme, point_id AS Point, raised_sprint_id AS Sprint FROM retro_action WHERE work_item_id = @itemId", new { itemId });
        link.ShouldBe((theme, pointId, world.Sprint));
        await watch.NextAsync<RetroChanged>(e => e.SprintIds.Contains(world.Sprint));
    }

    [Test]
    public async Task Create_WhenNoTitle_ShouldUseThePointText()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Pair on the hard ones."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { });

        (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("title").GetString().ShouldBe("Pair on the hard ones.");
    }

    [Test]
    public async Task Create_WhenAlreadyLinked_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Once."));
        var existing = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(existing, PointId: pointId));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await world.Connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @project AND type = 'action'", new { project = world.Project.Id })).ShouldBe(1);
    }

    [Test]
    public async Task Create_WhenEarlierActionWasRemoved_ShouldRepointTheLink()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Again."));
        var removed = await new ShipLean.TestSupport.Builders.WorkItemBuilder(world.Project.Id).WithType("action").WithStateNamed("Removed").InsertAsync(world.Connection);
        await world.Seed.ActionAsync(new RetroSeedAction(removed, PointId: pointId));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { });

        var created = (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("workItemId").GetGuid();
        (await world.Connection.QueryAsync<Guid>("SELECT work_item_id FROM retro_action WHERE point_id = @pointId", new { pointId })).ShouldBe([created]);
    }

    [Test]
    public async Task Create_WhenWentBadly_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.WentBadly, "Broke."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
