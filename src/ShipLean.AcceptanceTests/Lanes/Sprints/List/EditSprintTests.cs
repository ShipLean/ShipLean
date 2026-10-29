using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.Core;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.List;

// The Edit sprint rail opened from an All sprints row, on the seeded Storefront project (SHIP) signed in as Joel. Sprint 26
// is the last future sprint, so shortening it by a day touches no neighbour.
[TestFixture]
public class EditSprintTests : SeededAcceptanceTest
{
    [Test]
    public async Task Sprints_WhenAFutureSprintsEndIsMoved_ShouldShowTheNewDatesOnItsRow()
    {
        var before = await SprintDatabase.ReadDatesAsync(ConnectionString, SeedIds.Sprints.Sprint26);
        var newEnd = before.End.AddDays(-1);
        var sprints = new AllSprints(Page);
        await sprints.OpenAsync("SHIP");

        await sprints.EditButtonOf("Sprint 26").ClickAsync();
        await Expect(sprints.EditEnd).ToHaveValueAsync(before.End.ToString("yyyy-MM-dd"));
        await sprints.EditEnd.FillAsync(newEnd.ToString("yyyy-MM-dd"));
        var saved = await sprints.SaveEditAsync(SeedIds.Sprints.Sprint26);

        saved.Ok.ShouldBeTrue();
        var today = SprintLabels.Today(TimeProvider.System);
        await Expect(sprints.RowOf("Sprint 26")).ToContainTextAsync(SprintLabels.Dates(before.Start, newEnd, today));
        await Expect(sprints.EditRail).ToHaveCountAsync(0);
        (await SprintDatabase.ReadDatesAsync(ConnectionString, SeedIds.Sprints.Sprint26)).ShouldBe(new SprintDatabase.Dates(before.Start, newEnd));
    }
}
