using System.Net;
using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.Retros.People;

namespace ShipLean.WebApi.Tests.Retros.Api;

// The status codes and body shapes of the action item write routes (K6). The behaviour behind them is RetroActionWritesTests.
public sealed class RetroActionEndpointTests : RetroPeopleFixture
{
    private static readonly string[] DetailProperties = ["action", "points", "raisedIn", "theme"];
    private static readonly string[] ActionProperties = ["fixType", "key", "pointCount", "points", "sprint", "state", "title", "workItemId"];

    [Test]
    public async Task Create_WhenValid_ShouldBe201WithTheDetail()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/actions", new { title = "Fix", fixType = "skill", pointIds = new[] { await PointAsync(world) } });

        var body = await ReadAsync(response, HttpStatusCode.Created);
        PropertyNames(body).ShouldBe(DetailProperties);
        PropertyNames(body.GetProperty("action")).ShouldBe(ActionProperties);
    }

    [Test]
    public async Task Create_WhenBodyIsBad_ShouldBe400Validation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/actions", new { title = " ", fixType = "skill", pointIds = Array.Empty<Guid>() });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Create_WhenPointIsUnknown_ShouldBe404()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/actions", new { title = "Fix", fixType = "skill", pointIds = new[] { Guid.NewGuid() } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Create_WhenPointAlreadyHasAnAction_ShouldBe409()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);
        await world.Seed.ActionAsync(new RetroSeedAction(await world.ItemAsync("action", inSprint: false), PointId: pointId));

        using var response = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/actions", new { title = "Fix", fixType = "skill", pointIds = new[] { pointId } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task AddPoints_WhenValid_ShouldBe200WithTheDetail()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var item = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(item, FixType: RetroFixType.Guide, PointId: await PointAsync(world)));

        using var response = await Api.PostJsonAsync($"/api/retros/actions/{item}/points", new { pointIds = new[] { await PointAsync(world) } });

        var body = await ReadAsync(response, HttpStatusCode.OK);
        PropertyNames(body).ShouldBe(DetailProperties);
        body.GetProperty("points").GetArrayLength().ShouldBe(2);
    }

    [Test]
    public async Task AddPoints_WhenNoPoints_ShouldBe400Validation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var item = await world.ItemAsync("action", inSprint: false);

        using var response = await Api.PostJsonAsync($"/api/retros/actions/{item}/points", new { pointIds = Array.Empty<Guid>() });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task AddPoints_WhenActionIsUnknown_ShouldBe404()
    {
        using var response = await Api.PostJsonAsync($"/api/retros/actions/{Guid.NewGuid()}/points", new { pointIds = new[] { Guid.NewGuid() } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task AddPoints_WhenPointAlreadyHasAnAction_ShouldBe409()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var item = await world.ItemAsync("action", inSprint: false);
        var taken = await PointAsync(world);
        await world.Seed.ActionAsync(new RetroSeedAction(item, PointId: await PointAsync(world)));
        await world.Seed.ActionAsync(new RetroSeedAction(await world.ItemAsync("action", inSprint: false), PointId: taken));

        using var response = await Api.PostJsonAsync($"/api/retros/actions/{item}/points", new { pointIds = new[] { taken } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Put_WhenValid_ShouldBe200WithTheDetail()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var item = await world.ItemAsync("action", inSprint: false);
        await world.Seed.ActionAsync(new RetroSeedAction(item, FixType: RetroFixType.Guide, PointId: await PointAsync(world)));

        using var response = await Api.PutJsonAsync($"/api/retros/actions/{item}", new { fixType = "test", themeName = "Named" });

        var body = await ReadAsync(response, HttpStatusCode.OK);
        PropertyNames(body).ShouldBe(DetailProperties);
        body.GetProperty("action").GetProperty("fixType").GetString().ShouldBe("test");
    }

    [Test]
    public async Task Put_WhenThemeIdAndNameBothGiven_ShouldBe400Validation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var item = await world.ItemAsync("action", inSprint: false);

        using var response = await Api.PutJsonAsync($"/api/retros/actions/{item}", new { fixType = "test", themeId = Guid.NewGuid(), themeName = "Both" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenActionIsUnknown_ShouldBe404()
    {
        using var response = await Api.PutJsonAsync($"/api/retros/actions/{Guid.NewGuid()}", new { fixType = "test" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    private static async Task<Guid> PointAsync(World world)
        => await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "A point."));

    private static string[] PropertyNames(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
}
