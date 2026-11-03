using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

// GET api/retros/actions/{workItemId} while another request moves a point's link between the reader's two reads.
public sealed class ActionDetailRaceTests : RetroPeopleFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        var real = services.Single(descriptor => descriptor.ServiceType == typeof(IRetroCards));
        services.Remove(real);
        services.AddSingleton(provider => new RacingRetroCards((IRetroCards)ActivatorUtilities.CreateInstance(provider, real.ImplementationType!)));
        services.AddSingleton<IRetroCards>(provider => provider.GetRequiredService<RacingRetroCards>());
    };

    [Test]
    public async Task ActionDetail_WhenAPointIsTakenOverDuringTheRead_ShouldLeaveItOut()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var kept = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Kept."));
        var taken = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Taken over."));
        var itemId = await world.ItemAsync("action", inSprint: false);
        var other = await new WorkItemBuilder(world.Project.Id).WithType("action").InsertAsync(world.Connection);
        await world.Seed.ActionAsync(new RetroSeedAction(itemId, PointIds: [kept, taken]));
        await world.Seed.ActionAsync(new RetroSeedAction(other));
        Host.Services.GetRequiredService<RacingRetroCards>().BeforeLoad = () => world.Connection.ExecuteAsync(
            "UPDATE retro_action_point SET work_item_id = @other WHERE point_id = @taken AND archived_at IS NULL", new { other, taken });

        using var response = await Api.GetAsync($"/api/retros/actions/{itemId}");
        var detail = await ReadAsync(response, HttpStatusCode.OK);

        detail.GetProperty("points").EnumerateArray().Select(point => point.GetProperty("id").GetGuid()).ShouldBe([kept]);
        detail.GetProperty("action").GetProperty("pointCount").GetInt32().ShouldBe(1);
    }
}
