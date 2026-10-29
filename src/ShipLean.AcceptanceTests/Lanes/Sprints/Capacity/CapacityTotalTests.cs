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
    public async Task Capacity_WhenVelocityAndDaysEntered_ShouldShareVelocityAndSaveIt()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var seeded = await EntriesAsync(Page, sprint);
        seeded.Length.ShouldBeGreaterThan(0);
        var (workingDays, defaultDays) = await CalendarAsync(Page, sprint);
        var members = await MemberNamesAsync(Page, "SHIP");
        var added = members.Except(seeded.Select(e => e.Member)).First();
        var changed = seeded[0].Member;
        var committed = (await ReadAsync(Page, $"/api/sprints/{sprint}/metrics")).GetProperty("committed").GetDecimal();

        var capacity = new CapacityPage(Page);
        await capacity.OpenAsync("SHIP", sprint);
        await Expect(capacity.Rows).ToHaveCountAsync(seeded.Length);

        await capacity.Velocity.FillAsync("30");
        await capacity.Add.SelectOptionAsync(new SelectOptionValue { Label = added });
        await capacity.Days(changed).FillAsync("5");
        await capacity.Days(added).FillAsync("4");
        await capacity.Note(added).FillAsync("Part-time");

        var rows = seeded.Select(e => e.Member).Append(added).ToArray();
        var daysOf = new Dictionary<string, decimal> { [changed] = 5, [added] = 4 };
        decimal DaysOf(string member) => daysOf.GetValueOrDefault(member, defaultDays);
        var expected = rows.Sum(member => Share(30, rows.Length, DaysOf(member), workingDays));
        await Expect(capacity.Points(changed)).ToHaveTextAsync($"{Share(30, rows.Length, 5, workingDays):0.##}");
        await Expect(capacity.Total).ToHaveTextAsync($"{expected:0.##}");
        await Expect(capacity.KpiCapacity).ToHaveTextAsync($"{expected:0.##}");
        await Expect(capacity.KpiCommitted).ToHaveTextAsync($"{committed:0.##}");

        var saved = await capacity.SaveAsync(sprint);
        saved.Ok.ShouldBeTrue();
        await Expect(capacity.SaveButton).ToBeDisabledAsync();

        await Page.ReloadAsync();
        await Expect(capacity.Velocity).ToHaveValueAsync("30");
        await Expect(capacity.Total).ToHaveTextAsync($"{expected:0.##}");
        await Expect(capacity.Days(added)).ToHaveValueAsync("4");
        await Expect(capacity.Note(added)).ToHaveValueAsync("Part-time");
        var stored = (await ReadAsync(Page, $"/api/sprints/{sprint}/capacity")).GetProperty("teamTotal").GetDecimal();
        stored.ShouldBe(expected);

        // Removing someone shares the velocity among fewer people.
        await capacity.Remove(added).ClickAsync();
        await Expect(capacity.Rows).ToHaveCountAsync(seeded.Length);
        await Expect(capacity.Points(changed)).ToHaveTextAsync($"{Share(30, seeded.Length, 5, workingDays):0.##}");
    }
}
