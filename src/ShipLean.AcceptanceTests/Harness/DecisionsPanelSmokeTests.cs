using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Harness;

// Proves the Decisions page object and the seeding helper against the real app. J1a and J1b build on both.
[TestFixture]
public class DecisionsPanelSmokeTests : SeededAcceptanceTest
{
    [Test]
    public async Task DecisionsPanel_WhenItemHasNone_ShouldShowSection()
    {
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-147");
        var panel = new DecisionsPanel(Page, SeedIds.WorkItems.Ship147);

        await Expect(panel.Section).ToBeVisibleAsync();
        await Expect(panel.Empty).ToBeVisibleAsync();
    }
}
