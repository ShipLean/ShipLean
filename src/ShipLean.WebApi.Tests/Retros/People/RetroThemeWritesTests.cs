using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

public sealed class RetroThemeWritesTests : RetroPeopleFixture
{
    [Test]
    public async Task Rename_WhenDuplicateName_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await world.Seed.ThemeAsync("Slow CI");
        var other = await world.Seed.ThemeAsync("Handoffs");

        using var response = await Api.PutJsonAsync($"/api/retros/themes/{other}", new { name = "slow ci" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Rename_WhenFree_ShouldRenameAndMarkPerson()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var theme = await world.Seed.ThemeAsync("Old name");
        using var watch = Watch(world.Project.Id);

        using var response = await Api.PutJsonAsync($"/api/retros/themes/{theme}", new { name = "  New name " });

        (await ReadAsync(response, HttpStatusCode.OK)).GetProperty("name").GetString().ShouldBe("New name");
        (await world.Connection.QuerySingleAsync<string>("SELECT name_set_by FROM retro_theme WHERE id = @theme", new { theme })).ShouldBe("person");
        var published = await watch.NextAsync<RetroChanged>(_ => true);
        (published.SprintIds, published.WorkItemIds).ShouldBe(([], []));
    }

    [Test]
    public async Task Merge_WhenTwoSources_ShouldMovePointsAndFollowMergedInto()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var (a, b, target) = (await world.Seed.ThemeAsync("Alpha"), await world.Seed.ThemeAsync("Beta"), await world.Seed.ThemeAsync("Target"));
        var retro = await world.AgentRetroAsync();
        var pointA = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "A one.", ThemeId: a));
        var pointB = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "B one.", ThemeId: b));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "T one.", ThemeId: target));

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/themes/merge", new { sourceIds = new[] { a, b }, targetId = target });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var themes = (await world.Connection.QueryAsync<(string Name, Guid? Into, int Count, string Tone)>("SELECT name, merged_into_id AS Into, point_count AS Count, tone FROM retro_theme WHERE project_id = @project ORDER BY name", new { project = world.Project.Id })).ToList();
        themes.ShouldBe([("Alpha", target, 0, "bad"), ("Beta", target, 0, "bad"), ("Target", null, 3, "good")]);
        (await world.Connection.QueryAsync<Guid?>("SELECT theme_id FROM retro_point WHERE id = ANY(@ids)", new { ids = new[] { pointA, pointB } })).ShouldBe([target, target]);
        var later = await PostPointAsync(world.Project.Id, new { sprintId = world.Sprint, category = "improve", text = "Again.", themeName = "alpha" });
        later.GetProperty("theme").GetProperty("id").GetGuid().ShouldBe(target);
    }

    [Test]
    public async Task Merge_WhenSourceHasActionItem_ShouldRepointItToTarget()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var (source, target) = (await world.Seed.ThemeAsync("Source"), await world.Seed.ThemeAsync("Target"));
        var action = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(action, source));
        using var watch = Watch(world.Project.Id);

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/themes/merge", new { sourceIds = new[] { source }, targetId = target });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await world.Connection.QuerySingleAsync<Guid?>("SELECT theme_id FROM retro_action WHERE work_item_id = @action", new { action })).ShouldBe(target);
        var published = await watch.NextAsync<RetroChanged>(_ => true);
        (published.SprintIds, published.WorkItemIds).ShouldBe(([], []));
    }

    [Test]
    public async Task Merge_WhenThemeInAnotherProject_ShouldBeNotFound()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await using var other = await World.CreateAsync(await OpenDatabaseAsync());
        var (source, foreign) = (await world.Seed.ThemeAsync("Mine"), await other.Seed.ThemeAsync("Theirs"));

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/themes/merge", new { sourceIds = new[] { source }, targetId = foreign });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await world.Connection.QuerySingleAsync<Guid?>("SELECT merged_into_id FROM retro_theme WHERE id = @source", new { source })).ShouldBeNull();
    }

    [Test]
    public async Task List_ShouldHideEmptyAndMergedThemes()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var shown = await world.Seed.ThemeAsync("Zeta");
        var first = await world.Seed.ThemeAsync("Alpha");
        await world.Seed.ThemeAsync("Empty");
        var merged = await world.Seed.MergedThemeAsync("Merged", shown);
        var retro = await world.AgentRetroAsync();
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "One.", ThemeId: shown));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Two.", ThemeId: shown));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Three.", ThemeId: first));
        await world.Connection.ExecuteAsync("UPDATE retro_theme SET point_count = 1 WHERE id = @merged", new { merged });

        using var response = await Api.GetAsync($"/api/projects/{world.Project.Id}/retros/themes");

        var list = await ReadAsync(response, HttpStatusCode.OK);
        list.GetProperty("mode").GetString().ShouldBe("pick");
        list.GetProperty("themes").EnumerateArray().Select(t => (t.GetProperty("theme").GetProperty("name").GetString(), t.GetProperty("pointCount").GetInt32())).ShouldBe([("Alpha", 1), ("Zeta", 2)]);
    }
}
