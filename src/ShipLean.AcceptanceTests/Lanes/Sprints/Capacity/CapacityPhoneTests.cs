using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Capacity.CapacityApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Capacity;

// Sprint 24's Capacity on a phone (SHIP-726): the Save bar replaces the toolbar's Save, Discard reverts the draft, and
// Copy fills every row. The touch rules are checked on this task's regions only (summary, toolbar, first row).
[TestFixture]
public class CapacityPhoneTests : SeededAcceptanceTest
{
    private static readonly Guid Sprint = SeedIds.Sprints.Sprint24;

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Capacity_WhenPointsEditedOnPhone_ShouldSaveFromSaveBar()
    {
        var seeded = await EntriesAsync(Page, Sprint);
        var (workingDays, _) = await CalendarAsync(Page, Sprint);
        var velocity = (await ReadAsync(Page, $"/api/sprints/{Sprint}/capacity")).GetProperty("velocity");
        var typed = velocity.ValueKind == System.Text.Json.JsonValueKind.Null ? 30m : velocity.GetDecimal();
        var changed = seeded[0].Member;
        var (capacity, phone) = await OpenAsync();
        await Expect(phone.SaveBar).ToHaveCountAsync(0);
        await Expect(capacity.SaveButton).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, phone.Summary)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, phone.Toolbar)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, capacity.Rows.First)).ShouldBeEmpty();

        await capacity.Velocity.FillAsync($"{typed:0.##}");
        await capacity.Days(changed).FillAsync("5");

        var expected = seeded.Sum(e => Share(typed, seeded.Length, e.Member == changed ? 5 : e.Days ?? workingDays, workingDays));
        await Expect(phone.SaveBar).ToBeVisibleAsync();
        await Expect(capacity.Total).ToHaveTextAsync($"{expected:0.##}");
        await Expect(capacity.KpiCapacity).ToHaveTextAsync($"{expected:0.##}");

        (await phone.SaveAsync(Sprint)).Ok.ShouldBeTrue();
        await Expect(phone.SaveBar).ToHaveCountAsync(0);
        var stored = (await ReadAsync(Page, $"/api/sprints/{Sprint}/capacity")).GetProperty("teamTotal").GetDecimal();
        stored.ShouldBe(expected);
    }

    [Test]
    public async Task Capacity_WhenDiscardTappedOnPhone_ShouldRestoreSavedNumbers()
    {
        var seeded = await EntriesAsync(Page, Sprint);
        var (capacity, phone) = await OpenAsync();
        var savedTotal = await capacity.Total.InnerTextAsync();
        var savedVelocity = await capacity.Velocity.InputValueAsync();

        await capacity.Velocity.FillAsync("77");
        await capacity.Days(seeded[0].Member).FillAsync("1");
        await Expect(phone.SaveBar).ToBeVisibleAsync();

        await phone.Discard.TapAsync();

        await Expect(phone.SaveBar).ToHaveCountAsync(0);
        await Expect(capacity.Total).ToHaveTextAsync(savedTotal);
        await Expect(capacity.Velocity).ToHaveValueAsync(savedVelocity);
        await Expect(capacity.Days(seeded[0].Member)).ToHaveValueAsync(seeded[0].Days is { } d ? $"{d:0.#}" : "");
    }

    [Test]
    public async Task Capacity_WhenCopiedFromLastSprintOnPhone_ShouldFillEveryRow()
    {
        var previous = await EntriesAsync(Page, SeedIds.Sprints.Sprint23);
        previous.Length.ShouldBeGreaterThan(0);
        var (capacity, phone) = await OpenAsync();
        await Expect(capacity.Copy).ToContainTextAsync("Copy from Sprint 23");

        // Empty the table from the ⋯ sheet first, so what fills it has to come from the copy.
        await phone.More.TapAsync();
        await Page.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasText = "Clear" }).TapAsync();
        await Expect(capacity.Rows).ToHaveCountAsync(0);

        await capacity.Copy.TapAsync();

        await Expect(capacity.Rows).ToHaveCountAsync(previous.Length);
        foreach (var entry in previous)
        {
            await Expect(capacity.Days(entry.Member)).ToHaveValueAsync(entry.Days is { } typed ? $"{typed:0.#}" : "");
        }
    }

    private async Task<(CapacityPage Capacity, CapacityPhone Phone)> OpenAsync()
    {
        var capacity = new CapacityPage(Page);
        await capacity.OpenAsync("SHIP", Sprint);
        return (capacity, new CapacityPhone(Page));
    }
}
