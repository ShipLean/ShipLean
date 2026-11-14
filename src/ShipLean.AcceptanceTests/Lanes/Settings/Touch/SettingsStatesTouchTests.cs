using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Touch;

// The States tab on a phone (P5): a type select, two-line state rows with visible actions, and a stacked delete form.
// Joel is the admin; nothing here saves. The touch rules are checked on the States panel only.
[TestFixture]
public class SettingsStatesTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private ILocator Panel => Page.GetByTestId(SettingsTestIds.StatesPanel);

    [Test]
    public async Task States_WhenOpenedOnAPhone_ShouldShowTwoLineRowsWithVisibleActions()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();
        var select = Page.GetByTestId(SettingsTestIds.StateTypeSelect);
        await Expect(select).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(SettingsTestIds.StateTypeStory)).ToBeHiddenAsync();
        (await select.EvaluateAsync<string>("e => getComputedStyle(e).fontSize")).ShouldBe("16px");
        await Expect(states.Rows).ToHaveCountAsync(5);

        await select.SelectOptionAsync(new SelectOptionValue { Index = 4 });
        await Expect(select).ToHaveValueAsync("task");
        await Expect(states.Rows.First).ToBeVisibleAsync();
        await select.SelectOptionAsync(new SelectOptionValue { Index = 2 });
        await Expect(states.Rows).ToHaveCountAsync(5);

        for (var i = 0; i < 5; i++)
        {
            var row = states.Rows.Nth(i);
            foreach (var id in new[] { SettingsTestIds.StateMoveUp, SettingsTestIds.StateMoveDown, SettingsTestIds.StateDelete })
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
    public async Task States_WhenMovedUpOnAPhone_ShouldReorderTheDraft()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();
        await Expect(states.NameOf(0)).ToHaveValueAsync("New");
        await Expect(states.NameOf(1)).ToHaveValueAsync("Active");

        await states.Rows.Nth(1).GetByTestId(SettingsTestIds.StateMoveUp).TapAsync();

        await Expect(states.NameOf(0)).ToHaveValueAsync("Active");
        await Expect(states.NameOf(1)).ToHaveValueAsync("New");
        await Expect(Page.GetByTestId(SettingsTestIds.TabDirty)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task States_WhenDeletingOneWithItems_ShouldStackTheMoveForm()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();
        await states.DeleteOf(1).TapAsync();

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
