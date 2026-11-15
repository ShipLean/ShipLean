using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// The work item's description section on a phone against the real server: the View and Edit controls and the opened
// editor's toolbar follow the touch rules.
[TestFixture]
public class ItemDescriptionTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Description_WhenEditOpenedOnPhone_ShouldFollowTheTouchRules()
    {
        var screen = new WorkItemScreen(Page);
        await screen.OpenAsync("SHIP", "SHIP-141");
        var description = Page.GetByTestId(WorkItemTestIds.Description);
        (await TouchRules.ViolationsAsync(Page, description)).ShouldBeEmpty();

        await Page.GetByTestId(WorkItemTestIds.DescriptionEdit).TapAsync();

        await Expect(Page.GetByTestId(WorkItemTestIds.DescriptionInput)).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, description)).ShouldBeEmpty();
    }
}
