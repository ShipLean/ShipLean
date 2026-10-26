using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Capacity.CapacityApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Capacity;

// The seeded Storefront's current Sprint 24, signed in as Joel. The test saves, so the class has its own database.
[TestFixture]
public class CapacityTotalTests : SeededAcceptanceTest
{
    [Test]
    public async Task Capacity_WhenMembersEnterPoints_ShouldCompareTeamTotalOnly()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var seeded = await EntriesAsync(Page, sprint);
        seeded.Length.ShouldBeGreaterThan(0);
        var members = await MemberNamesAsync(Page, "SHIP");
        var blank = members.Except(seeded.Select(e => e.Member)).First();
        var changed = seeded[0];
        var committed = (await ReadAsync(Page, $"/api/sprints/{sprint}/metrics")).GetProperty("committed").GetDecimal();

        var capacity = new CapacityPage(Page);
        await capacity.OpenAsync("SHIP", sprint);
        await Expect(capacity.Rows).ToHaveCountAsync(members.Length);
        await Expect(capacity.Total).ToHaveTextAsync($"{seeded.Sum(e => e.Points):0.##}");

        await capacity.Points(blank).FillAsync("2");
        await capacity.Points(changed.Member).FillAsync("9");
        await capacity.Note(blank).FillAsync("Part-time");

        var expected = seeded.Sum(e => e.Points) - changed.Points + 9 + 2;
        await Expect(capacity.Total).ToHaveTextAsync($"{expected:0.##}");
        await Expect(capacity.KpiCapacity).ToHaveTextAsync($"{expected:0.##}");
        await Expect(capacity.KpiCommitted).ToHaveTextAsync($"{committed:0.##}");

        var saved = await capacity.SaveAsync(sprint);
        saved.Ok.ShouldBeTrue();
        await Expect(capacity.SaveButton).ToBeDisabledAsync();

        await Page.ReloadAsync();
        await Expect(capacity.Total).ToHaveTextAsync($"{expected:0.##}");
        await Expect(capacity.Points(blank)).ToHaveValueAsync("2");
        await Expect(capacity.Note(blank)).ToHaveValueAsync("Part-time");
        var stored = (await ReadAsync(Page, $"/api/sprints/{sprint}/capacity")).GetProperty("teamTotal").GetDecimal();
        stored.ShouldBe(expected);
    }
}
