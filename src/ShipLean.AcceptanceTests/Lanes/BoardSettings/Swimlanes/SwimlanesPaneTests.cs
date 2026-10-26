using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.BoardSettings;

namespace ShipLean.AcceptanceTests.Lanes.BoardSettings.Swimlanes;

// The Swimlanes pane of the Storefront stories board, from the seed: Expedite (priority = 1) and the Default lane.
// Only the last test saves a change that sticks, so the earlier ones still see the seeded lanes.
[TestFixture]
public class SwimlanesPaneTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task BoardSettings_WhenDefaultLaneDeleted_ShouldRefuse()
    {
        var pane = new SwimlanesPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.Rows).ToHaveCountAsync(2);

        await pane.Delete(1).ClickAsync();

        await Expect(pane.Refusal).ToContainTextAsync("The default lane can't be deleted");
        await Expect(pane.Rows).ToHaveCountAsync(2);
        await Expect(pane.Names.Nth(1)).ToHaveValueAsync("Default lane");
    }

    [Test]
    [Order(2)]
    public async Task BoardSettings_WhenLaneAdded_ShouldSaveCriteria()
    {
        var pane = new SwimlanesPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.Rows).ToHaveCountAsync(2);

        await pane.Add.ClickAsync();
        await Expect(pane.Rows).ToHaveCountAsync(3);
        await pane.Names.Nth(2).FillAsync("Urgent");
        await pane.CriteriaMode(1).ClickAsync();
        await pane.ClauseValue(1).SelectOptionAsync(new SelectOptionValue { Label = "3" });
        await pane.SaveAndWaitAsync();

        await pane.OpenAsync("SHIP");
        await Expect(pane.Rows).ToHaveCountAsync(3);
        await Expect(pane.Names.Nth(2)).ToHaveValueAsync("Urgent");
        await Expect(pane.ClauseValue(1)).ToHaveValueAsync("3");
    }
}
