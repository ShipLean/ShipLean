using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

public sealed class RetroPointWritesTests : RetroPeopleFixture
{
    [Test]
    public async Task AddPoint_WhenSprintOnly_ShouldCreateSprintLevelPersonRetro()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        using var watch = Watch(world.Project.Id);

        var point = await PostPointAsync(world.Project.Id, new { sprintId = world.Sprint, category = "improve", text = "Fewer meetings." });

        point.GetProperty("canArchive").GetBoolean().ShouldBeTrue();
        point.GetProperty("workItem").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        var retro = await world.Connection.QuerySingleAsync<(string Role, Guid? Sprint, Guid? Item, Guid Author)>(
            "SELECT role, sprint_id AS Sprint, work_item_id AS Item, author_user_id AS Author FROM retrospective WHERE project_id = @project", new { project = world.Project.Id });
        retro.ShouldBe(("person", world.Sprint, null, SeedIds.Users.Joel));
        var published = await watch.NextAsync<RetroChanged>(e => e.SprintIds.Contains(world.Sprint));
        published.WorkItemIds.ShouldBeEmpty();
    }

    [Test]
    public async Task AddPoint_WhenWorkItem_ShouldReuseOwnRetro()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var story = await world.ItemAsync("story");

        await PostPointAsync(world.Project.Id, new { workItemId = story, category = "wentWell", text = "Pairing." });
        await PostPointAsync(world.Project.Id, new { workItemId = story, category = "improve", text = "Smaller steps." });

        var retros = (await world.Connection.QueryAsync<Guid?>("SELECT sprint_id FROM retrospective WHERE work_item_id = @story AND role = 'person'", new { story })).ToList();
        retros.ShouldBe([world.Sprint]);
        (await world.Connection.QuerySingleAsync<int>("SELECT count(*)::int FROM retro_point p JOIN retrospective r ON r.id = p.retrospective_id WHERE r.work_item_id = @story", new { story })).ShouldBe(2);
    }

    [Test]
    public async Task AddPoint_WhenNewThemeName_ShouldCreateTheme()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());

        var point = await PostPointAsync(world.Project.Id, new { sprintId = world.Sprint, category = "wentBadly", text = "CI was slow.", themeName = "Slow CI" });

        point.GetProperty("theme").GetProperty("name").GetString().ShouldBe("Slow CI");
        point.GetProperty("themeSetBy").GetString().ShouldBe("person");
        var theme = await world.Connection.QuerySingleAsync<(string NameSetBy, int Count, string Tone)>(
            "SELECT name_set_by AS NameSetBy, point_count AS Count, tone FROM retro_theme WHERE project_id = @project AND name = 'Slow CI'", new { project = world.Project.Id });
        theme.ShouldBe(("person", 1, "bad"));
    }

    [Test]
    public async Task AddPoint_WhenRetroIsAnAgents_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var retro = await world.AgentRetroAsync();

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/points", new { retroId = retro, category = "improve", text = "x" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task AddPoint_WhenSprintOfAnotherProject_ShouldBeNotFound()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await using var other = await World.CreateAsync(await OpenDatabaseAsync());

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/points", new { sprintId = other.Sprint, category = "improve", text = "x" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task MoveTheme_WhenAgentPoint_ShouldMoveAndMarkPerson()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var from = await world.Seed.ThemeAsync("From");
        var to = await world.Seed.ThemeAsync("To");
        var retro = await world.AgentRetroAsync();
        var pointId = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Slow.", ThemeId: from));
        using var watch = Watch(world.Project.Id);

        using var response = await Api.PutJsonAsync($"/api/retros/points/{pointId}/theme", new { themeId = to });

        var point = await ReadAsync(response, HttpStatusCode.OK);
        point.GetProperty("theme").GetProperty("id").GetGuid().ShouldBe(to);
        point.GetProperty("canArchive").GetBoolean().ShouldBeFalse();
        var row = await world.Connection.QuerySingleAsync<(Guid Theme, string SetBy)>("SELECT theme_id AS Theme, theme_set_by AS SetBy FROM retro_point WHERE id = @pointId", new { pointId });
        row.ShouldBe((to, "person"));
        var counts = await world.Connection.QueryAsync<(string Name, int Count)>("SELECT name, point_count FROM retro_theme WHERE id = ANY(@ids) ORDER BY name", new { ids = new[] { from, to } });
        counts.ShouldBe([("From", 0), ("To", 1)]);
        (await watch.NextAsync<RetroChanged>(e => e.SprintIds.Contains(world.Sprint))).ProjectId.ShouldBe(world.Project.Id);
    }

    [Test]
    public async Task MoveTheme_ShouldNotChangeText()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var to = await world.Seed.ThemeAsync("To");
        var retro = await world.AgentRetroAsync();
        var pointId = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Original text.", "Original cause."));

        using var response = await Api.PutJsonAsync($"/api/retros/points/{pointId}/theme", new { themeName = "to" });

        await ReadAsync(response, HttpStatusCode.OK);
        var row = await world.Connection.QuerySingleAsync<(string Text, string Cause, Guid Theme)>("SELECT text, root_cause AS Cause, theme_id AS Theme FROM retro_point WHERE id = @pointId", new { pointId });
        row.ShouldBe(("Original text.", "Original cause.", to));
    }

    [Test]
    public async Task Archive_WhenAgentPoint_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var retro = await world.AgentRetroAsync();
        var pointId = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Fine."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/archive");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Archive_WhenOwnPoint_ShouldHideFromThemes()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var point = await PostPointAsync(world.Project.Id, new { sprintId = world.Sprint, category = "wentBadly", text = "Gone soon.", themeName = "Short lived" });
        using var watch = Watch(world.Project.Id);

        using var response = await Api.PostJsonAsync($"/api/retros/points/{point.GetProperty("id").GetGuid()}/archive");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var themes = await Api.GetAsync($"/api/projects/{world.Project.Id}/retros/themes");
        (await ReadAsync(themes, HttpStatusCode.OK)).GetProperty("themes").GetArrayLength().ShouldBe(0);
        await watch.NextAsync<RetroChanged>(e => e.SprintIds.Contains(world.Sprint));
    }
}
