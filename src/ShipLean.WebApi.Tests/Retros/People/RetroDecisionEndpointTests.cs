using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
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

    [Test]
    public async Task Routes_WhenCallerNotMember_ShouldBeNotFound()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "X.", NotNeededReason: "R."));
        Host.ActAs(SeedIds.Users.Kai);
        var access = Host.Services.GetRequiredService<IProjectAccess>();

        var error = await Should.ThrowAsync<ApiException>(() => access.RequireAsync(ProjectItem.RetroPoint, pointId, ProjectRole.Member));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("not-needed")]
    [TestCase("undo-not-needed")]
    public async Task Routes_WhenPointUnknown_ShouldBeNotFound(string route)
    {
        using var response = await Api.PostJsonAsync($"/api/retros/points/{Guid.NewGuid()}/{route}", new { reason = "R." });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
