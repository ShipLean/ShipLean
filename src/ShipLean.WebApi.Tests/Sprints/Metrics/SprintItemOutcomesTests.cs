using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Metrics;

// ISprintMetrics.GetSprintItemsAsync over the seeded SHIP history (db/seed/README.md lists who was carried and added).
[TestFixture]
public class SprintItemOutcomesTests : ApiFixture
{
    [Test]
    public async Task GetSprintItems_WhenSeededShip_ShouldListCarriedAndAddedItems()
    {
        (Guid Sprint, string[] Carried, string[] Added)[] expected =
        [
            (SeedIds.Sprints.Sprint19, ["SHIP-34"], []),
            (SeedIds.Sprints.Sprint20, ["SHIP-39"], ["SHIP-40"]),
            (SeedIds.Sprints.Sprint21, [], []),
            (SeedIds.Sprints.Sprint22, ["SHIP-49"], ["SHIP-50"]),
            (SeedIds.Sprints.Sprint23, ["SHIP-138"], []),
        ];

        using var scope = Host.Services.CreateScope();
        var metrics = scope.ServiceProvider.GetRequiredService<ISprintMetrics>();
        foreach (var (sprint, carried, added) in expected)
        {
            var items = await metrics.GetSprintItemsAsync(sprint);
            items.Where(i => i.CarriedOver).Select(i => i.Key).ShouldBe(carried, ignoreOrder: true);
            items.Where(i => i.AddedMidSprint).Select(i => i.Key).ShouldBe(added, ignoreOrder: true);
        }
    }

    [Test]
    public async Task GetSprintItems_WhenSeededShip_ShouldSumToTheSprintMetrics()
    {
        using var scope = Host.Services.CreateScope();
        var metrics = scope.ServiceProvider.GetRequiredService<ISprintMetrics>();
        foreach (var sprint in new[] { SeedIds.Sprints.Sprint19, SeedIds.Sprints.Sprint22, SeedIds.Sprints.Sprint23 })
        {
            var items = await metrics.GetSprintItemsAsync(sprint);
            var figures = await metrics.GetSprintAsync(sprint);
            items.Where(i => i.Committed).Sum(i => i.Points ?? 0).ShouldBe(figures.Committed);
            items.Where(i => i.Completed).Sum(i => i.Points ?? 0).ShouldBe(figures.Completed);
        }
    }

    [Test]
    public async Task GetSprintItems_WhenUnknownSprint_ShouldBeNotFound()
    {
        using var scope = Host.Services.CreateScope();

        await Should.ThrowAsync<ShipLean.Contracts.Errors.ApiException>(
            scope.ServiceProvider.GetRequiredService<ISprintMetrics>().GetSprintItemsAsync(Guid.NewGuid()));
    }
}
