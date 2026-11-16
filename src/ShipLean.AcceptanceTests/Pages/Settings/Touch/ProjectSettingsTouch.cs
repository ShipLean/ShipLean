using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings.Touch;

// Project settings of Storefront (SHIP) at 900px and below (SHIP-732, SHIP-1809 P1-P3): the pane index, each pane a
// sub-page with ‹ back to it, the shared SaveBar with its change list, and the General and Members panes.
public sealed class ProjectSettingsTouch(IPage page)
{
    public ILocator Name => page.GetByTestId(SettingsTestIds.GeneralName);

    public ILocator TabRow => page.GetByTestId(SettingsTestIds.TabRow);

    public ILocator Index => page.GetByTestId(SettingsTestIds.SettingsIndex);

    public ILocator IndexRows => page.GetByTestId(SettingsTestIds.SettingsIndexRow);

    // A pane's row by its name, which starts the row's text ("States", not the "…states…" in another row's line).
    public ILocator IndexRow(string pane) =>
        IndexRows.Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(pane)}") });

    public ILocator Back => page.GetByTestId(TouchTestIds.TopBarBack);

    public ILocator Crumbs => page.GetByTestId(ShellTestIds.Crumb);

    public ILocator SaveBar => page.GetByTestId(TouchTestIds.SaveBar);

    public ILocator SaveBarSave => page.GetByTestId(TouchTestIds.SaveBarSave);

    public ILocator SaveBarDiscard => page.GetByTestId(TouchTestIds.SaveBarDiscard);

    // The SaveBar's message, which opens the change list.
    public ILocator Unsaved => page.GetByTestId(SettingsTestIds.Unsaved);

    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator ChangeItems => Sheet.GetByTestId(SettingsTestIds.ChangeItem);

    public ILocator MembersPanel => page.GetByTestId(SettingsTestIds.MembersPanel);

    public ILocator MemberRow(string email) =>
        page.GetByTestId(SettingsTestIds.MemberRow).Filter(new() { HasText = email });

    public ILocator Tab(string id) => page.GetByTestId($"settings-tab-{id}");

    // A pane, opened by its address: a sub-page with ‹ back.
    public async Task OpenAsync(string tab = "general")
    {
        await page.GotoAsync(Links.ProjectSettings("SHIP", tab));
        await Assertions.Expect(Back).ToBeVisibleAsync();
    }

    // The tab-less address: the pane index.
    public async Task OpenIndexAsync()
    {
        await page.GotoAsync(Links.ProjectSettingsIndex("SHIP"));
        await Assertions.Expect(IndexRows.First).ToBeVisibleAsync();
    }

    // ‹ back to the index, then into another pane, as a phone moves between panes.
    public async Task GoToPaneAsync(string tab)
    {
        await Back.TapAsync();
        await IndexRow(tab).TapAsync();
        await Assertions.Expect(Back).ToBeVisibleAsync();
    }

    public async Task<bool> PageFitsAsync() =>
        await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth");

    public static async Task<string> FontSizeAsync(ILocator element) =>
        await element.EvaluateAsync<string>("el => getComputedStyle(el).fontSize");
}
