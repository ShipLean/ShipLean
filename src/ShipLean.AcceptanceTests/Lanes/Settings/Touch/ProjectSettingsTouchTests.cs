using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Touch;

// Project settings of Storefront on a phone (SHIP-732 P1-P4, L732a; SHIP-1809 P1-P3). Joel is its only admin; Priya is a
// member. The touch rules are checked on the index and the Members panel; TouchRouteSweepTests checks each address.
[TestFixture]
public class ProjectSettingsTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    // States, Integrations and Demos are other lanes' content (L732b, L732c): their own tests check them.
    private static readonly (string Tab, string Pane)[] OwnPanes =
        [("general", "General"), ("members", "Members"), ("sprints", "Sprints & forecast")];

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
        await settings.GoToPaneAsync("Members");
        await settings.MemberRow("priya@shiplean.local").GetByTestId(SettingsTestIds.MemberRemove).TapAsync();
        await settings.GoToPaneAsync("Sprints & forecast");

        await settings.Unsaved.TapAsync();

        await Expect(settings.ChangeItems).ToHaveCountAsync(2);
        await settings.ChangeItems.Filter(new() { HasText = "priya@shiplean.local" }).TapAsync();
        await Expect(settings.Sheet).ToHaveCountAsync(0);
        await Expect(Page).ToHaveURLAsync(new Regex("/p/SHIP/settings/members$"));
        await Expect(settings.MembersPanel).ToBeVisibleAsync();
    }

    [Test]
    public async Task Index_WhenOpenedOnAPhone_ShouldListEveryPaneAndOpenEachAsASubPage()
    {
        var settings = new ProjectSettingsTouch(Page);
        await settings.OpenIndexAsync();
        await Expect(settings.Crumbs).ToHaveTextAsync(["SHIP", "Settings"]);
        await Expect(settings.IndexRows).ToHaveTextAsync([
            new Regex("^General"), new Regex("^States"), new Regex("^Members"), new Regex("^Sprints & forecast"),
            new Regex("^Integrations"), new Regex("^Demos"), new Regex("^Inspection"), new Regex("^Prompts"),
        ]);
        await Expect(settings.IndexRow("Members").GetByTestId(SettingsTestIds.SettingsIndexCount)).ToHaveTextAsync("6");
        await Expect(settings.TabRow).ToHaveCountAsync(0);
        await Expect(settings.Back).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, settings.Index)).ShouldBeEmpty();

        foreach (var (tab, pane) in OwnPanes)
        {
            await settings.IndexRow(pane).TapAsync();
            await Expect(Page).ToHaveURLAsync(new Regex($"/p/SHIP/settings/{tab}$"));
            await Expect(settings.Crumbs).ToHaveTextAsync(["SHIP", "Settings", pane]);
            await Expect(settings.TabRow).ToHaveCountAsync(0);
            (await settings.PageFitsAsync()).ShouldBeTrue($"the {tab} pane scrolls the page sideways");

            await settings.Back.TapAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/p/SHIP/settings$"));
        }
    }

    [Test]
    public async Task Index_WhenThePaneHasUnsavedEdits_ShouldKeepTheDraftAndMarkThePane()
    {
        var settings = new ProjectSettingsTouch(Page);
        await settings.OpenAsync();
        // Not "Storefront Two": the General test saves that name on the class's shared seed.
        await settings.Name.FillAsync("Storefront Draft");

        await settings.Back.TapAsync();

        await Expect(settings.IndexRow("General").GetByTestId(SettingsTestIds.TabDirty)).ToBeVisibleAsync();
        await Expect(settings.IndexRow("Members").GetByTestId(SettingsTestIds.TabDirty)).ToHaveCountAsync(0);
        await Expect(settings.Unsaved).ToHaveTextAsync("1 unsaved change");

        await settings.IndexRow("General").TapAsync();
        await Expect(settings.Name).ToHaveValueAsync("Storefront Draft");
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

    // The settings dialog frame (board and Taskboard settings) names its project in the header at touch (SHIP-1987).
    [Test]
    public async Task SettingsDialog_WhenOpenedOnAPhone_ShouldNameTheProjectInTheHeader()
    {
        await Page.GotoAsync(Links.BoardSettings("SHIP", "stories", "styles"));

        // The class shares one seed, and the General test saves the name "Storefront Two".
        await Expect(Page.GetByTestId(SettingsDialogTestIds.ProjectChip))
            .ToHaveTextAsync(new Regex("^Storefront( Two)? · SHIP$"));
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
