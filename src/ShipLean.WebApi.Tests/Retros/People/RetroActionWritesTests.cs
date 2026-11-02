using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

// Contract K5: the action item writes that cover one or more points, through the routes of K6.
public sealed class RetroActionWritesTests : RetroPeopleFixture
{
    [Test]
    public async Task CreateAction_WhenPointsFromTwoRetros_ShouldLinkBoth()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var secondSprint = await RetroArrangement.SprintAsync(world.Connection, world.Project.Id, "S2", new DateOnly(2026, 1, 19), new DateOnly(2026, 2, 1));
        var first = await PointAsync(world);
        var second = await world.Seed.PointAsync(
            await world.Seed.RetroAsync(new RetroSeedRetro(ShipLean.TestSupport.SeedIds.Users.Joel, RetroRole.SubAgent, SprintId: secondSprint, AgentName: $"agent-{Guid.NewGuid():N}")),
            new RetroSeedPoint(RetroCategory.WentBadly, "Second."));
        using var watch = Watch(world.Project.Id);

        using var response = await CreateAsync(world, [first, second]);

        var detail = await ReadAsync(response, HttpStatusCode.Created);
        detail.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("id").GetGuid()).ShouldBe([first, second]);
        detail.GetProperty("action").GetProperty("pointCount").GetInt32().ShouldBe(2);
        detail.GetProperty("raisedIn").GetProperty("id").GetGuid().ShouldBe(world.Sprint);
        var itemId = detail.GetProperty("action").GetProperty("workItemId").GetGuid();
        (await world.Connection.QueryAsync<Guid>("SELECT point_id FROM retro_action_point WHERE work_item_id = @itemId AND archived_at IS NULL ORDER BY position", new { itemId })).ShouldBe([first, second]);
        var changed = await watch.NextAsync<RetroChanged>(e => e.SprintIds.Contains(world.Sprint));
        changed.SprintIds.ShouldContain(secondSprint);
    }

    [Test]
    public async Task CreateAction_ShouldStoreFixType()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());

        using var response = await CreateAsync(world, [await PointAsync(world)], fixType: "analyzer");

        var detail = await ReadAsync(response, HttpStatusCode.Created);
        detail.GetProperty("action").GetProperty("fixType").GetString().ShouldBe("analyzer");
        detail.GetProperty("action").GetProperty("state").GetProperty("name").GetString().ShouldBe("New");
        var itemId = detail.GetProperty("action").GetProperty("workItemId").GetGuid();
        (await world.Connection.QuerySingleAsync<string>("SELECT fix_type FROM retro_action WHERE work_item_id = @itemId", new { itemId })).ShouldBe("analyzer");
        (await world.Connection.QuerySingleAsync<(Guid? Sprint, Guid? Parent)>("SELECT sprint_id AS Sprint, parent_id AS Parent FROM work_item WHERE id = @itemId", new { itemId })).ShouldBe((null, null));
    }

    [Test]
    public async Task CreateAction_WhenNoTheme_ShouldTakeTheCommonest()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var rare = await world.Seed.ThemeAsync("Rare");
        var common = await world.Seed.ThemeAsync("Common");

        using var response = await CreateAsync(world, [await PointAsync(world, theme: rare), await PointAsync(world, theme: common), await PointAsync(world, theme: common)]);

        (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("theme").GetProperty("id").GetGuid().ShouldBe(common);
    }

    [Test]
    public async Task CreateAction_WhenThemesTie_ShouldTakeTheFirstPoints()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var alpha = await world.Seed.ThemeAsync("Alpha");
        var beta = await world.Seed.ThemeAsync("Beta");
        var inAlpha = await PointAsync(world, theme: alpha);
        var inBeta = await PointAsync(world, theme: beta);

        using var response = await CreateAsync(world, [inBeta, inAlpha]);

        (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("theme").GetProperty("id").GetGuid().ShouldBe(beta);
    }

    [Test]
    public async Task CreateAction_WhenThemeNamed_ShouldUseItInsteadOfThePointsTheme()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointsTheme = await world.Seed.ThemeAsync("Points theme");

        using var response = await CreateAsync(world, [await PointAsync(world, theme: pointsTheme)], themeName: "Fresh theme");

        (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("theme").GetProperty("name").GetString().ShouldBe("Fresh theme");
    }

    [Test]
    public async Task CreateAction_WhenNotInPlan_ShouldLink()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world, RetroCategory.NotInPlan);

        using var response = await CreateAsync(world, [pointId]);

        (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("points")[0].GetProperty("decision").GetString().ShouldBe("actioned");
    }

    [Test]
    public async Task CreateAction_WhenWentWell_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());

        using var response = await CreateAsync(world, [await PointAsync(world, RetroCategory.WentWell)]);

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "pointIds"));
        (await ActionCountAsync(world)).ShouldBe(0);
    }

    [Test]
    public async Task CreateAction_WhenNoPoints_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());

        using var response = await CreateAsync(world, []);

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "pointIds"));
    }

    [Test]
    public async Task CreateAction_WhenDuplicatePointIds_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);

        using var response = await CreateAsync(world, [pointId, pointId]);

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "pointIds"));
    }

    [Test]
    public async Task CreateAction_WhenPointAlreadyDecided_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var free = await PointAsync(world);
        var taken = await PointAsync(world);
        await world.Seed.ActionAsync(new RetroSeedAction(await world.ItemAsync("action", inSprint: false), PointId: taken));

        using var response = await CreateAsync(world, [free, taken]);

        (await ErrorAsync(response, HttpStatusCode.Conflict)).ShouldBe((ApiErrorCodes.Conflict, null));
        (await world.Connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @project AND type = 'action'", new { project = world.Project.Id })).ShouldBe(1);
        (await world.Connection.QuerySingleAsync<int>("SELECT count(*)::int FROM retro_action_point WHERE point_id = @free", new { free })).ShouldBe(0);
    }

    [Test]
    public async Task CreateAction_WhenPointNotNeeded_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Skip.", NotNeededReason: "Already fixed."));

        using var response = await CreateAsync(world, [pointId]);

        (await ErrorAsync(response, HttpStatusCode.Conflict)).ShouldBe((ApiErrorCodes.Conflict, null));
        (await ActionCountAsync(world)).ShouldBe(0);
    }

    [Test]
    public async Task CreateAction_WhenEarlierActionRemoved_ShouldArchiveItsLinkAndLinkTheNew()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);
        var removed = await new ShipLean.TestSupport.Builders.WorkItemBuilder(world.Project.Id).WithType("action").WithStateNamed("Removed").InsertAsync(world.Connection);
        await world.Seed.ActionAsync(new RetroSeedAction(removed, PointId: pointId));

        using var response = await CreateAsync(world, [pointId]);

        var created = (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("action").GetProperty("workItemId").GetGuid();
        var rows = await world.Connection.QueryAsync<(Guid Item, bool Archived)>("SELECT work_item_id AS Item, archived_at IS NOT NULL AS Archived FROM retro_action_point WHERE point_id = @pointId ORDER BY archived_at NULLS LAST", new { pointId });
        rows.ShouldBe([(removed, true), (created, false)]);
    }

    [Test]
    public async Task CreateAction_WhenTwoRaceForOnePoint_ShouldGiveOneConflictAndRemoveTheLosersItem()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => await CreateAsync(world, [pointId])));

        try
        {
            responses.Select(response => response.StatusCode).Order().ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict]);
            var states = await world.Connection.QueryAsync<string>(
                "SELECT st.category FROM work_item w JOIN state st ON st.id = w.state_id WHERE w.project_id = @project AND w.type = 'action' ORDER BY st.category", new { project = world.Project.Id });
            states.ShouldBe(["proposed", "removed"]);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Test]
    public async Task CreateAction_WhenPointMarkedNotNeededAfterTheCheck_ShouldConflictAndRemoveTheItem()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);
        // Stands in for a Not needed write that lands after the early check: when the action item is inserted, the point is marked.
        var name = $"notneeded_{Guid.NewGuid():N}";
        await world.Connection.ExecuteAsync(
            $"""
            CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              UPDATE retro_point SET not_needed_reason = 'Late.', decided_by_user_id = created_by_user_id, decided_at = now() WHERE id = '{pointId}';
              RETURN NEW; END $$;
            CREATE TRIGGER {name} AFTER INSERT ON work_item FOR EACH ROW WHEN (NEW.project_id = '{world.Project.Id}') EXECUTE FUNCTION {name}();
            """);
        try
        {
            using var response = await CreateAsync(world, [pointId]);

            (await ErrorAsync(response, HttpStatusCode.Conflict)).ShouldBe((ApiErrorCodes.Conflict, null));
        }
        finally
        {
            await world.Connection.ExecuteAsync($"DROP TRIGGER {name} ON work_item; DROP FUNCTION {name}();");
        }

        (await world.Connection.QuerySingleAsync<int>("SELECT count(*)::int FROM retro_action_point WHERE point_id = @pointId", new { pointId })).ShouldBe(0);
        (await world.Connection.QuerySingleAsync<string>("SELECT st.category FROM work_item w JOIN state st ON st.id = w.state_id WHERE w.project_id = @project AND w.type = 'action'", new { project = world.Project.Id })).ShouldBe("removed");
    }

    [Test]
    public async Task AddPoints_ShouldLinkTheNewPointsAfterTheOldOnes()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var first = await PointAsync(world);
        var added = await PointAsync(world, RetroCategory.NotInPlan);
        var itemId = await CreatedItemAsync(world, [first]);

        using var response = await AddPointsAsync(itemId, [added]);

        var detail = await ReadAsync(response, HttpStatusCode.OK);
        detail.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("id").GetGuid()).ShouldBe([first, added]);
        detail.GetProperty("action").GetProperty("pointCount").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task AddPoints_ShouldNotChangeTheTheme()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var kept = await world.Seed.ThemeAsync("Kept");
        var other = await world.Seed.ThemeAsync("Other");
        var itemId = await CreatedItemAsync(world, [await PointAsync(world, theme: kept)]);

        using var response = await AddPointsAsync(itemId, [await PointAsync(world, theme: other), await PointAsync(world, theme: other)]);

        (await ReadAsync(response, HttpStatusCode.OK)).GetProperty("theme").GetProperty("id").GetGuid().ShouldBe(kept);
        (await world.Connection.QuerySingleAsync<Guid>("SELECT theme_id FROM retro_action WHERE work_item_id = @itemId", new { itemId })).ShouldBe(kept);
    }

    [Test]
    public async Task AddPoints_WhenItemIsNotAction_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var story = await world.ItemAsync("story");

        using var response = await AddPointsAsync(story, [await PointAsync(world)]);

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "workItemId"));
    }

    [Test]
    public async Task AddPoints_WhenNoPoints_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world)]);

        using var response = await AddPointsAsync(itemId, []);

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "pointIds"));
    }

    [Test]
    public async Task AddPoints_WhenDuplicatePointIds_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world)]);
        var pointId = await PointAsync(world);

        using var response = await AddPointsAsync(itemId, [pointId, pointId]);

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "pointIds"));
    }

    [Test]
    public async Task AddPoints_WhenPointActioned_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var mine = await PointAsync(world);
        var itemId = await CreatedItemAsync(world, [mine]);
        var elsewhere = await PointAsync(world);
        await CreatedItemAsync(world, [elsewhere]);

        using var response = await AddPointsAsync(itemId, [elsewhere]);

        (await ErrorAsync(response, HttpStatusCode.Conflict)).ShouldBe((ApiErrorCodes.Conflict, null));
    }

    [Test]
    public async Task AddPoints_WhenPointNotNeeded_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world)]);
        var skipped = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Skip.", NotNeededReason: "Already fixed."));

        using var response = await AddPointsAsync(itemId, [skipped]);

        (await ErrorAsync(response, HttpStatusCode.Conflict)).ShouldBe((ApiErrorCodes.Conflict, null));
    }

    [Test]
    public async Task AddPoints_WhenWentWell_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world)]);

        using var response = await AddPointsAsync(itemId, [await PointAsync(world, RetroCategory.WentWell)]);

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "pointIds"));
    }

    [Test]
    public async Task RemovingTheActionItem_ShouldReturnAllItsPointsToUndecided()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var retro = await world.AgentRetroAsync();
        var first = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "One."));
        var second = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Two."));
        var itemId = await CreatedItemAsync(world, [first, second]);
        var cards = Host.Services.GetRequiredService<IRetroCards>();
        (await cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Select(point => point.Decision).ShouldBe([RetroDecision.Actioned, RetroDecision.Actioned]);

        await world.Connection.ExecuteAsync(
            "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @project AND type = 'action' AND category = 'removed' LIMIT 1) WHERE id = @itemId",
            new { project = world.Project.Id, itemId });

        var points = (await cards.LoadAsync([retro], CancellationToken.None)).Single().Points;
        points.Select(point => point.Decision).ShouldBe([RetroDecision.Undecided, RetroDecision.Undecided]);
        points.ShouldAllBe(point => point.Action == null);
    }

    [Test]
    public async Task Writes_WhenPointInOtherProject_ShouldBeNotFound()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await using var other = await World.CreateAsync(await OpenDatabaseAsync());
        var foreign = await PointAsync(other);
        var itemId = await CreatedItemAsync(world, [await PointAsync(world)]);

        using var create = await CreateAsync(world, [foreign]);
        using var add = await AddPointsAsync(itemId, [foreign]);

        (await ErrorAsync(create, HttpStatusCode.NotFound)).ShouldBe((ApiErrorCodes.NotFound, null));
        (await ErrorAsync(add, HttpStatusCode.NotFound)).ShouldBe((ApiErrorCodes.NotFound, null));
        (await ActionCountAsync(other)).ShouldBe(0);
    }

    [Test]
    public async Task UpdateAction_ShouldStoreTheNewFixType()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var theme = await world.Seed.ThemeAsync("Theme");
        var itemId = await CreatedItemAsync(world, [await PointAsync(world, theme: theme)], fixType: "skill");

        using var response = await UpdateAsync(itemId, new { fixType = "guide", themeId = theme });

        var detail = await ReadAsync(response, HttpStatusCode.OK);
        detail.GetProperty("action").GetProperty("fixType").GetString().ShouldBe("guide");
        (await world.Connection.QuerySingleAsync<string>("SELECT fix_type FROM retro_action WHERE work_item_id = @itemId", new { itemId })).ShouldBe("guide");
        detail.GetProperty("theme").GetProperty("id").GetGuid().ShouldBe(theme);
    }

    [Test]
    public async Task UpdateAction_ShouldChangeTheThemeAndKeepTheFixType()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world, theme: await world.Seed.ThemeAsync("Old"))], fixType: "test");
        var other = await world.Seed.ThemeAsync("New");
        using var watch = Watch(world.Project.Id);

        using var response = await UpdateAsync(itemId, new { fixType = "test", themeId = other });

        var detail = await ReadAsync(response, HttpStatusCode.OK);
        detail.GetProperty("theme").GetProperty("id").GetGuid().ShouldBe(other);
        detail.GetProperty("action").GetProperty("fixType").GetString().ShouldBe("test");
        await watch.NextAsync<RetroChanged>(e => e.SprintIds.Contains(world.Sprint));
    }

    [Test]
    public async Task UpdateAction_WhenLegacyFixTypeStaysNull_ShouldChangeTheTheme()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(itemId, PointId: await PointAsync(world)));

        using var response = await UpdateAsync(itemId, new { fixType = (string?)null, themeName = "Legacy theme" });

        var detail = await ReadAsync(response, HttpStatusCode.OK);
        detail.GetProperty("theme").GetProperty("name").GetString().ShouldBe("Legacy theme");
        detail.GetProperty("action").TryGetProperty("fixType", out var fixType).ShouldBeTrue();
        fixType.ValueKind.ShouldBe(JsonValueKind.Null);
        (await world.Connection.QuerySingleAsync<string?>("SELECT fix_type FROM retro_action WHERE work_item_id = @itemId", new { itemId })).ShouldBeNull();
    }

    [Test]
    public async Task UpdateAction_WhenBothThemesNull_ShouldClearTheTheme()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world, theme: await world.Seed.ThemeAsync("Gone"))], fixType: "skill");

        using var response = await UpdateAsync(itemId, new { fixType = "skill", themeId = (Guid?)null, themeName = (string?)null });

        var detail = await ReadAsync(response, HttpStatusCode.OK);
        detail.GetProperty("theme").ValueKind.ShouldBe(JsonValueKind.Null);
        (await world.Connection.QuerySingleAsync<Guid?>("SELECT theme_id FROM retro_action WHERE work_item_id = @itemId", new { itemId })).ShouldBeNull();
    }

    [Test]
    public async Task UpdateAction_WhenFixTypeNullOverAStoredOne_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world)], fixType: "skill");

        using var response = await UpdateAsync(itemId, new { fixType = (string?)null });

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "fixType"));
        (await world.Connection.QuerySingleAsync<string>("SELECT fix_type FROM retro_action WHERE work_item_id = @itemId", new { itemId })).ShouldBe("skill");
    }

    [Test]
    public async Task UpdateAction_WhenThemeIdAndName_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world)], fixType: "skill");

        using var response = await UpdateAsync(itemId, new { fixType = "skill", themeId = await world.Seed.ThemeAsync("T"), themeName = "T" });

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "themeId"));
    }

    [Test]
    public async Task UpdateAction_WhenThemeIdUnknown_ShouldBeNotFound()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var itemId = await CreatedItemAsync(world, [await PointAsync(world)], fixType: "skill");

        using var response = await UpdateAsync(itemId, new { fixType = "skill", themeId = Guid.NewGuid() });

        (await ErrorAsync(response, HttpStatusCode.NotFound)).ShouldBe((ApiErrorCodes.NotFound, null));
    }

    [Test]
    public async Task UpdateAction_WhenItemIsNotAction_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var story = await world.ItemAsync("story");

        using var response = await UpdateAsync(story, new { fixType = "skill" });

        (await ErrorAsync(response, HttpStatusCode.BadRequest)).ShouldBe((ApiErrorCodes.Validation, "workItemId"));
    }

    private static Task<Guid> PointAsync(World world, RetroCategory category = RetroCategory.Improve, Guid? theme = null)
        => PointInRetroAsync(world, world.AgentRetroAsync(), category, theme);

    private static async Task<Guid> PointInRetroAsync(World world, Task<Guid> retro, RetroCategory category, Guid? theme)
        => await world.Seed.PointAsync(await retro, new RetroSeedPoint(category, "A point.", ThemeId: theme));

    private Task<HttpResponseMessage> CreateAsync(World world, Guid[] pointIds, string fixType = "skill", Guid? themeId = null, string? themeName = null)
        => Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/actions", new { title = "Fix it", fixType, pointIds, themeId, themeName });

    private Task<HttpResponseMessage> AddPointsAsync(Guid workItemId, Guid[] pointIds) => Api.PostJsonAsync($"/api/retros/actions/{workItemId}/points", new { pointIds });

    private Task<HttpResponseMessage> UpdateAsync(Guid workItemId, object body) => Api.PutJsonAsync($"/api/retros/actions/{workItemId}", body);

    private async Task<Guid> CreatedItemAsync(World world, Guid[] pointIds, string fixType = "skill")
    {
        using var response = await CreateAsync(world, pointIds, fixType);
        return (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("action").GetProperty("workItemId").GetGuid();
    }

    private static async Task<(string Code, string? Field)> ErrorAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        var error = await ApiClient.ReadApiErrorAsync(response, status);
        return (error.Code, error.Field);
    }

    private static Task<int> ActionCountAsync(World world)
        => world.Connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @project AND type = 'action'", new { project = world.Project.Id });
}
