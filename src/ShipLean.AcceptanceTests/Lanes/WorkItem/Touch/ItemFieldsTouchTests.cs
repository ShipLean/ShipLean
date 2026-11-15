using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.AcceptanceTests.Pages.WorkItem.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// The work item's fields and pickers on a phone against the real server (SHIP-714, L714b): State and Parent open as
// bottom sheets, picking applies and closes, and Parent's search waits for a tap. The touch rules are checked on this
// task's regions only (the fields block and its sheets); the header, sections and rail are other tasks'.
[TestFixture]
public class ItemFieldsTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task State_WhenPickedFromSheetOnPhone_ShouldShowInSaveDraft()
    {
        var screen = new WorkItemScreen(Page);
        var fields = new FieldSheets(Page);
        var touch = new TouchShell(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        (await TouchRules.ViolationsAsync(Page, fields.Fields)).ShouldBeEmpty();

        await fields.StateButton.TapAsync();

        var sheet = touch.Sheet("State");
        await Expect(sheet).ToBeVisibleAsync();
        // The desktop's groups: every story state, each with its category.
        await Expect(fields.StateOptions).ToHaveCountAsync(5);
        await Expect(fields.StateOption("Active")).ToContainTextAsync("In progress");
        await Expect(fields.StateOption("Closed")).ToContainTextAsync("Completed");
        (await TouchRules.ViolationsAsync(Page, sheet)).ShouldBeEmpty();
        await fields.StateOption("Active").TapAsync();

        await Expect(touch.Sheets).ToHaveCountAsync(0);
        await Expect(fields.StateButton).ToContainTextAsync("Active");
        await Expect(fields.ChangedDots).ToHaveCountAsync(1);
        await Expect(touch.SaveBarMessage).ToContainTextAsync("State");
    }

    [Test]
    public async Task Parent_WhenSearchedInSheetOnPhone_ShouldSetParent()
    {
        var screen = new WorkItemScreen(Page);
        var fields = new FieldSheets(Page);
        var touch = new TouchShell(Page);
        await screen.OpenAsync("SHIP", "SHIP-165");

        await fields.ParentButton.TapAsync();

        var sheet = touch.Sheet("Parent");
        await Expect(sheet).ToBeVisibleAsync();
        await Expect(fields.ParentSearch).ToBeVisibleAsync();
        await Expect(fields.ParentSearch).Not.ToBeFocusedAsync();
        await fields.ParentSearch.TapAsync();
        await Expect(fields.ParentSearch).ToBeFocusedAsync();
        await fields.ParentSearch.FillAsync("sign-in");
        await Expect(fields.ParentOption("Reporting")).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, sheet)).ShouldBeEmpty();
        await fields.ParentOption("Mobile sign-in").TapAsync();

        await Expect(touch.Sheets).ToHaveCountAsync(0);
        await Expect(fields.ParentButton).ToContainTextAsync("Mobile sign-in");
        await Expect(touch.SaveBarMessage).ToContainTextAsync("Parent");
        var saved = await Page.RunAndWaitForApiAsync(
            $"/api/work-items/{SeedIds.WorkItems.Ship165}", "PATCH", () => touch.SaveBarSave.TapAsync());
        saved.Ok.ShouldBeTrue();
        var item = await WorkItemWrites.GetAsync(Page, "SHIP-165");
        item.GetProperty("parent").GetProperty("key").GetString().ShouldBe("SHIP-102");
    }
}
