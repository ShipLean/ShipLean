using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.List;

// A Storefront (SHIP) whose members have entered no capacity anywhere: the arrangement clears the seed's entries, so this
// class has its own database.
[TestFixture]
public class VelocityLabelTests : SeededAcceptanceTest
{
    [Test]
    public async Task Sprints_WhenNoCapacity_ShouldShowVelocityLabel()
    {
        await SprintDatabase.ArchiveAllCapacityAsync(ConnectionString);
        var sprints = new AllSprints(Page);
        await sprints.OpenAsync("SHIP");

        await Expect(sprints.Rows).ToHaveCountAsync(8);
        await Expect(sprints.CapacityCells).ToHaveCountAsync(8);
        var velocity = await sprints.VelocityAverage.GetAttributeAsync("data-value");
        velocity.ShouldNotBeNull();
        foreach (var cell in await sprints.CapacityCells.AllAsync())
        {
            await Expect(cell).ToHaveTextAsync($"{velocity} · velocity");
        }

        await Expect(sprints.VelocityBars).ToHaveCountAsync(6);
    }
}
