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
        var link = await world.Connection.QuerySingleAsync<(Guid Theme, Guid Point, Guid Sprint)>("SELECT ra.theme_id AS Theme, rap.point_id AS Point, ra.raised_sprint_id AS Sprint FROM retro_action ra JOIN retro_action_point rap ON rap.work_item_id = ra.work_item_id WHERE ra.work_item_id = @itemId AND rap.archived_at IS NULL", new { itemId });
        link.ShouldBe((theme, pointId, world.Sprint));
        var placement = await world.Connection.QuerySingleAsync<(Guid? Sprint, Guid? Parent)>("SELECT sprint_id AS Sprint, parent_id AS Parent FROM work_item WHERE id = @itemId", new { itemId });
        placement.ShouldBe((null, null));
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
    public async Task Create_WhenEarlierActionWasRemoved_ShouldTakeTheLinkOver()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Again."));
        var removed = await new ShipLean.TestSupport.Builders.WorkItemBuilder(world.Project.Id).WithType("action").WithStateNamed("Removed").InsertAsync(world.Connection);
        await world.Seed.ActionAsync(new RetroSeedAction(removed, PointId: pointId));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { });

        var created = (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("workItemId").GetGuid();
        (await world.Connection.QueryAsync<Guid>("SELECT work_item_id FROM retro_action_point WHERE point_id = @pointId AND archived_at IS NULL", new { pointId })).ShouldBe([created]);
    }

    [Test]
    public async Task Create_WhenRepointLosesTheRace_ShouldConflictAndRemoveTheNewItem()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Racing."));
        var removed = await new ShipLean.TestSupport.Builders.WorkItemBuilder(world.Project.Id).WithType("action").WithStateNamed("Removed").InsertAsync(world.Connection);
        var other = await new ShipLean.TestSupport.Builders.WorkItemBuilder(world.Project.Id).WithType("action").InsertAsync(world.Connection);
        await world.Seed.ActionAsync(new RetroSeedAction(removed, PointId: pointId));
        await world.Seed.ActionAsync(new RetroSeedAction(other));
        // Stands in for the other request: when this request's action item is inserted, the link moves to a different, live item.
        await world.Connection.ExecuteAsync(
            """
            CREATE FUNCTION retro_link_race() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              UPDATE retro_action_point SET work_item_id = (
                  SELECT ra.work_item_id FROM retro_action ra JOIN work_item w ON w.id = ra.work_item_id JOIN state st ON st.id = w.state_id
                  WHERE ra.project_id = NEW.project_id AND st.category <> 'removed' AND ra.work_item_id <> NEW.id LIMIT 1)
              WHERE archived_at IS NULL AND work_item_id IN (
                  SELECT ra.work_item_id FROM retro_action ra JOIN work_item w ON w.id = ra.work_item_id JOIN state st ON st.id = w.state_id
                  WHERE ra.project_id = NEW.project_id AND st.category = 'removed');
              RETURN NEW; END $$;
            CREATE TRIGGER retro_link_race AFTER INSERT ON work_item FOR EACH ROW WHEN (NEW.title = 'Link race') EXECUTE FUNCTION retro_link_race();
            """);
        try
        {
            using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { title = "Link race" });

            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        }
        finally
        {
            await world.Connection.ExecuteAsync("DROP TRIGGER IF EXISTS retro_link_race ON work_item; DROP FUNCTION IF EXISTS retro_link_race();");
        }

        var states = await world.Connection.QueryAsync<string>(
            "SELECT st.category FROM work_item w JOIN state st ON st.id = w.state_id WHERE w.project_id = @project AND w.type = 'action' AND w.id NOT IN (@removed, @other)",
            new { project = world.Project.Id, removed, other });
        states.ShouldBe(["removed"]);
        (await world.Connection.QuerySingleAsync<Guid>("SELECT work_item_id FROM retro_action_point WHERE point_id = @pointId AND archived_at IS NULL", new { pointId })).ShouldBe(other);
    }

    [Test]
    public async Task Create_WhenWentBadly_ShouldCreate()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.WentBadly, "Broke."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { });

        var itemId = (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("workItemId").GetGuid();
        (await world.Connection.QuerySingleAsync<Guid>("SELECT work_item_id FROM retro_action_point WHERE point_id = @pointId AND archived_at IS NULL", new { pointId })).ShouldBe(itemId);
    }

    [Test]
    public async Task Create_WhenWentWell_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.WentWell, "Smooth."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/action", new { });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
