using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Touch;

// The States tab on a phone (P5; SHIP-1809 P4/P5): a row of type chips, state rows with a ⠿ handle and a ⋯ sheet of actions,
// and a stacked delete form.
// Joel is the admin; nothing here saves. The touch rules are checked on the States panel only.
[TestFixture]
public class SettingsStatesTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private ILocator Panel => Page.GetByTestId(SettingsTestIds.StatesPanel);

    private ILocator SheetRow(string label) =>
        Page.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasTextString = label });

    [Test]
    public async Task States_WhenOpenedOnAPhone_ShouldShowRowsWithAHandleAndAMoreButton()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();
        await Expect(Page.GetByTestId(SettingsTestIds.StateTypeSelect)).ToHaveCountAsync(0);
        var story = Page.GetByTestId(SettingsTestIds.StateTypeStory);
        await Expect(story).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(states.Rows).ToHaveCountAsync(5);

        var task = Page.GetByTestId(SettingsTestIds.StateTypeTask);
        await task.TapAsync();
        await Expect(task).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(states.Rows.First).ToBeVisibleAsync();
        await story.TapAsync();
        await Expect(states.Rows).ToHaveCountAsync(5);
        foreach (var chip in new[] { story, task, Page.GetByTestId(SettingsTestIds.StateAddOpen) })
            (await chip.BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);

        for (var i = 0; i < 5; i++)
        {
            var row = states.Rows.Nth(i);
            foreach (var id in new[] { SettingsTestIds.StateMoveUp, SettingsTestIds.StateMoveDown, SettingsTestIds.StateDelete })
                await Expect(row.GetByTestId(id)).ToBeHiddenAsync();
            foreach (var id in new[] { SettingsTestIds.StateHandle, TouchTestIds.MoreButton })
            {
                var button = row.GetByTestId(id);
                await Expect(button).ToBeVisibleAsync();
                var box = await button.BoundingBoxAsync();
                box!.Width.ShouldBeGreaterThanOrEqualTo(32);
                box.Height.ShouldBeGreaterThanOrEqualTo(32);
            }

            var name = await states.NameOf(i).BoundingBoxAsync();
            var category = await states.CategoryOf(i).BoundingBoxAsync();
            category!.Y.ShouldBeGreaterThan(name!.Y + name.Height - 1);
        }

        (await Panel.EvaluateAsync<bool>("e => e.scrollWidth <= e.clientWidth")).ShouldBeTrue();
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
        (await TouchRules.ViolationsAsync(Page, Panel)).ShouldBeEmpty();
    }

    [Test]
    public async Task States_WhenMovedUpFromTheMoreSheetOnAPhone_ShouldReorderTheDraft()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();
        await Expect(states.NameOf(0)).ToHaveValueAsync("New");
        await Expect(states.NameOf(1)).ToHaveValueAsync("Active");

        await states.Rows.Nth(1).GetByTestId(TouchTestIds.MoreButton).TapAsync();
        await SheetRow("Move up").TapAsync();

        await Expect(states.NameOf(0)).ToHaveValueAsync("Active");
        await Expect(states.NameOf(1)).ToHaveValueAsync("New");
        // At touch the pane has no tab row (SHIP-1977), so the draft's change shows in the save bar.
        await Expect(Page.GetByTestId(SettingsTestIds.Unsaved)).ToHaveTextAsync("1 unsaved change");
    }

    [Test]
    public async Task States_WhenDeletingOneWithItems_ShouldStackTheMoveForm()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();
        await states.Rows.Nth(1).GetByTestId(TouchTestIds.MoreButton).TapAsync();
        await SheetRow("Delete…").TapAsync();

        await Expect(states.DeleteForm).ToBeVisibleAsync();
        var target = await Page.GetByTestId(SettingsTestIds.StateDeleteTarget).BoundingBoxAsync();
        var cancel = await Page.GetByTestId(SettingsTestIds.StateDeleteCancel).BoundingBoxAsync();
        var confirm = await states.DeleteConfirm.BoundingBoxAsync();
        Math.Abs(cancel!.Width - confirm!.Width).ShouldBeLessThanOrEqualTo(1);
        cancel.Y.ShouldBe(confirm.Y);
        cancel.Y.ShouldBeGreaterThan(target!.Y + target.Height - 1);
        (await TouchRules.ViolationsAsync(Page, Panel)).ShouldBeEmpty();

        await Page.GetByTestId(SettingsTestIds.StateDeleteCancel).TapAsync();
        await Expect(states.DeleteForm).ToHaveCountAsync(0);
        await Expect(states.Rows).ToHaveCountAsync(5);
    }
}
