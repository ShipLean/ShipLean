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
    public async Task Capacity_WhenCopied_ShouldFillFromPreviousSprint()
    {
        var previous = await EntriesAsync(Page, SeedIds.Sprints.Sprint23);
        previous.Length.ShouldBeGreaterThan(0);
        var members = await MemberNamesAsync(Page, "SHIP");
        var capacity = new CapacityPage(Page);
        await capacity.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(capacity.Copy).ToHaveTextAsync("Copy from Sprint 23");

        // Empty the rows first, so what fills them has to come from the copy.
        await capacity.Clear.ClickAsync();
        foreach (var member in members)
        {
            await Expect(capacity.Points(member)).ToHaveValueAsync(string.Empty);
        }

        await capacity.Copy.ClickAsync();

        foreach (var entry in previous)
        {
            await Expect(capacity.Points(entry.Member)).ToHaveValueAsync($"{entry.Points:0.##}");
        }

        foreach (var member in members.Except(previous.Select(e => e.Member)))
        {
            await Expect(capacity.Points(member)).ToHaveValueAsync(string.Empty);
        }

        await Expect(capacity.Total).ToHaveTextAsync($"{previous.Sum(e => e.Points):0.##}");
        (await capacity.SaveAsync(SeedIds.Sprints.Sprint24)).Ok.ShouldBeTrue();
        await Expect(capacity.SaveButton).ToBeDisabledAsync();
        var saved = await EntriesAsync(Page, SeedIds.Sprints.Sprint24);
        saved.Select(e => (e.Member, e.Points))
            .ShouldBe(previous.Select(e => (e.Member, e.Points)).ToArray(), ignoreOrder: true);
    }
}
