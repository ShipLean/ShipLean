using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Capacity.CapacityApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Capacity;

// Sprint 24 after Sprint 23, as seeded. The test saves, so the class has its own database.
[TestFixture]
public class CapacityCopyTests : SeededAcceptanceTest
{
    [Test]
    public async Task Capacity_WhenCopied_ShouldFillTheTableFromPreviousSprint()
    {
        var previous = await EntriesAsync(Page, SeedIds.Sprints.Sprint23);
        previous.Length.ShouldBeGreaterThan(0);
        var (_, defaultDays) = await CalendarAsync(Page, SeedIds.Sprints.Sprint24);
        var capacity = new CapacityPage(Page);
        await capacity.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(capacity.Copy).ToHaveTextAsync("Copy from Sprint 23");

        // Empty the table first, so what fills it has to come from the copy.
        await capacity.Clear.ClickAsync();
        await Expect(capacity.Rows).ToHaveCountAsync(0);
        await Expect(capacity.Empty).ToContainTextAsync("No capacity entered: planning uses velocity");

        await capacity.Copy.ClickAsync();

        await Expect(capacity.Rows).ToHaveCountAsync(previous.Length);
        foreach (var entry in previous)
        {
            await Expect(capacity.Days(entry.Member)).ToHaveValueAsync($"{entry.Days ?? defaultDays:0.#}");
        }

        // The seeded sprints carry no velocity, so the one the team types is saved with the days.
        await capacity.Velocity.FillAsync("30");
        (await capacity.SaveAsync(SeedIds.Sprints.Sprint24)).Ok.ShouldBeTrue();
        await Expect(capacity.SaveButton).ToBeDisabledAsync();
        var saved = await EntriesAsync(Page, SeedIds.Sprints.Sprint24);
        saved.Select(e => (e.Member, e.Days))
            .ShouldBe(previous.Select(e => (e.Member, e.Days)).ToArray(), ignoreOrder: true);
    }
}
