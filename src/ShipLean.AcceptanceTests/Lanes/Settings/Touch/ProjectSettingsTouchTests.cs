using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Touch;

// Project settings of Storefront on a phone (SHIP-732 P1-P4, L732a). Joel is its only admin; Priya is a member. The touch
// rules are checked on this task's regions (the tab row and the Members panel) only.
[TestFixture]
public class ProjectSettingsTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    // States, Integrations and Demos are other lanes' content (L732b, L732c): their own tests check them.
    private static readonly string[] OwnTabs = ["general", "members", "sprints"];

    [Test]
    public async Task General_WhenEditedOnAPhone_ShouldSaveFromTheSaveBar()
    {
        var settings = new ProjectSettingsTouch(Page);
        await settings.OpenAsync();
        await Expect(settings.SaveBar).ToHaveCountAsync(0);

        (await ProjectSettingsTouch.FontSizeAsync(settings.Name)).ShouldBe("16px");
        var field = await settings.Name.BoundingBoxAsync();
        var panelWidth = await settings.Name.EvaluateAsync<double>("el => el.closest('section').getBoundingClientRect().width");
        ((double)field!.Width).ShouldBeGreaterThan(panelWidth - 40);

        await settings.Name.FillAsync("Storefront Two");
        await Expect(settings.SaveBar).ToBeVisibleAsync();
        await Expect(settings.Unsaved).ToHaveTextAsync("1 unsaved change");

        await Page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/settings", "PUT", () => settings.SaveBarSave.TapAsync());
        await Expect(settings.SaveBar).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(settings.Name).ToHaveValueAsync("Storefront Two");
    }

    [Test]
    public async Task SaveBar_WhenDiscarded_ShouldRestoreTheDraft()
    {
        var settings = new ProjectSettingsTouch(Page);
        await settings.OpenAsync();
        var before = await settings.Name.InputValueAsync();

        await settings.Name.FillAsync("Something else");
        await settings.SaveBarDiscard.TapAsync();

        await Expect(settings.Name).ToHaveValueAsync(before);
        await Expect(settings.SaveBar).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ChangeList_WhenTappedOnAPhone_ShouldJumpToTheTab()
    {
        var settings = new ProjectSettingsTouch(Page);
        await settings.OpenAsync();
        await settings.Name.FillAsync("Storefront Two");
        await settings.Tab("members").TapAsync();
        await settings.MemberRow("priya@shiplean.local").GetByTestId(SettingsTestIds.MemberRemove).TapAsync();
        await settings.Tab("sprints").TapAsync();

        await settings.Unsaved.TapAsync();

        await Expect(settings.ChangeItems).ToHaveCountAsync(2);
        await settings.ChangeItems.Filter(new() { HasText = "priya@shiplean.local" }).TapAsync();
        await Expect(settings.Sheet).ToHaveCountAsync(0);
        await Expect(settings.Tab("members")).ToHaveAttributeAsync("aria-current", "page");
    }

    [Test]
    public async Task Tabs_WhenOnAPhone_ShouldScrollInTheirOwnBox()
    {
        var settings = new ProjectSettingsTouch(Page);
        await settings.OpenAsync();
        var sizes = await settings.TabRow.EvaluateAsync<int[]>("el => [el.scrollWidth, el.clientWidth]");
        sizes[0].ShouldBeGreaterThan(sizes[1]);
        // The row scrolls in its own box by design, so the touch rules are checked on its tabs, not on the row.
        (await TouchRules.ViolationsAsync(Page, settings.Tab("general"))).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, settings.Tab("demos"))).ShouldBeEmpty();

        foreach (var tab in OwnTabs)
        {
            await settings.OpenAsync(tab);
            await Expect(settings.Tab(tab)).ToHaveAttributeAsync("aria-current", "page");
            (await settings.PageFitsAsync()).ShouldBeTrue($"the {tab} tab scrolls the page sideways");
        }
    }

    [Test]
    public async Task Members_WhenOnAPhone_ShouldShowRemoveWithoutHover()
    {
        var settings = new ProjectSettingsTouch(Page);
        await settings.OpenAsync("members");
        var row = settings.MemberRow("priya@shiplean.local");
        var remove = row.GetByTestId(SettingsTestIds.MemberRemove);

        await Expect(remove).ToBeVisibleAsync();
        (await remove.BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);
        (await TouchRules.ViolationsAsync(Page, settings.MembersPanel)).ShouldBeEmpty();

        await remove.TapAsync();
        var undo = row.GetByTestId(SettingsTestIds.MemberUndo);
        await Expect(undo).ToBeVisibleAsync();
        (await undo.BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);

        await undo.TapAsync();
        await Expect(row.GetByTestId(SettingsTestIds.MemberRemove)).ToBeVisibleAsync();
        await Expect(settings.SaveBar).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Settings_WhenAMemberOpensThemOnAPhone_ShouldShowNoSaveBar()
    {
        await SignOutAsync();
        await SignInAsync(TestUser.Priya.Email!, TestUser.Priya.Password!);
        var settings = new ProjectSettingsTouch(Page);
        await settings.OpenAsync();

        await Expect(Page.GetByTestId(SettingsTestIds.ReadOnlyBanner)).ToBeVisibleAsync();
        await Expect(settings.Name).ToBeDisabledAsync();
        await Expect(settings.SaveBar).ToHaveCountAsync(0);
    }
}
