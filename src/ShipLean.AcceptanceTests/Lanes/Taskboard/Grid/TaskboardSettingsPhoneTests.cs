using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Journeys.BoardSettings;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Grid;

// Taskboard settings on a phone (SHIP-725, L725d), opened from the address (?settings=fields) so the test does not depend on
// the toolbar. Sprint 24 of the seeded Storefront project, signed in as Joel. The Taskboard applies each change at once, so
// the SaveBar stays away unless a save fails (open question Q3 on SHIP-707: when it is ruled to wait for Save, this
// test becomes a Save-from-the-bar test).
[TestFixture]
public class TaskboardSettingsPhoneTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task TaskboardSettings_WhenFieldChangedOnPhone_ShouldApplyAtOnceWithoutASaveBar()
    {
        var settingsPath = $"/api/boards/{SeedIds.Boards.ShipTaskboard}/settings";
        var dialog = new TaskboardSettingsDialog(Page);
        await Page.GotoAsync(Links.Taskboard("SHIP", SeedIds.Sprints.Sprint24) + "?settings=fields");
        await Expect(dialog.FieldsPane).ToBeVisibleAsync();

        (await TouchRules.ViolationsAsync(Page, dialog.Dialog)).ShouldBeEmpty();
        await Expect(dialog.NavItem("fields")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Page.GetByTestId(TouchTestIds.SaveBar)).ToHaveCountAsync(0);

        var saved = await Page.RunAndWaitForApiAsync(settingsPath, "PUT", () => dialog.ShowPoints.CheckAsync());

        saved.Ok.ShouldBeTrue(await saved.TextAsync());
        await Expect(Page.GetByTestId(TouchTestIds.SaveBar)).ToHaveCountAsync(0);

        await dialog.NavItem("styles").ClickAsync();
        await Expect(dialog.StylesPane).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, dialog.Dialog)).ShouldBeEmpty();

        await Page.ReloadAsync();
        await Expect(dialog.NavItem("styles")).ToHaveAttributeAsync("aria-current", "page");
        await dialog.NavItem("fields").ClickAsync();
        await Expect(dialog.ShowPoints).ToBeCheckedAsync();
    }
}
