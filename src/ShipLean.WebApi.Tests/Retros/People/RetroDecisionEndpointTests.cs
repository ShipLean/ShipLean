using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

public sealed class RetroDecisionEndpointTests : RetroPeopleFixture
{
    [Test]
    public async Task NotNeeded_ShouldReturn200WithThePointInK4Shape()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.WentBadly, "Slow."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/not-needed", new { reason = "Known." });

        var body = await ReadAsync(response, HttpStatusCode.OK);
        body.GetProperty("id").GetGuid().ShouldBe(pointId);
        body.GetProperty("decision").GetString().ShouldBe("notNeeded");
        var notNeeded = body.GetProperty("notNeeded");
        notNeeded.GetProperty("reason").GetString().ShouldBe("Known.");
        notNeeded.GetProperty("by").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object);
        notNeeded.GetProperty("decidedAtLabel").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task NotNeeded_WhenNoBody_ShouldBeBadRequest()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "X."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/not-needed", new { });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("reason");
    }

    [Test]
    public async Task Undo_ShouldReturn200WithUndecidedPoint()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "X.", NotNeededReason: "R."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/undo-not-needed");

        var body = await ReadAsync(response, HttpStatusCode.OK);
        body.GetProperty("decision").GetString().ShouldBe("undecided");
        (body.TryGetProperty("notNeeded", out var notNeeded) ? notNeeded.ValueKind : System.Text.Json.JsonValueKind.Null).ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [TestCase("not-needed")]
    [TestCase("undo-not-needed")]
    public async Task Routes_WhenCallerNotMember_ShouldBeNotFoundAndChangeNothing(string route)
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "X.", NotNeededReason: "R."));

        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync($"/api/retros/points/{pointId}/{route}", new { reason = "Other." });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await world.Connection.QuerySingleAsync<string?>("SELECT not_needed_reason FROM retro_point WHERE id = @pointId", new { pointId })).ShouldBe("R.");
    }

    [TestCase("not-needed")]
    [TestCase("undo-not-needed")]
    public async Task Routes_WhenPointUnknown_ShouldBeNotFound(string route)
    {
        using var response = await Api.PostJsonAsync($"/api/retros/points/{Guid.NewGuid()}/{route}", new { reason = "R." });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
