using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Settings.Touch;

// Project settings of Storefront (SHIP) at 900px and below (SHIP-732): the tab row, the shared SaveBar with its change
// list, and the General and Members tabs.
public sealed class ProjectSettingsTouch(IPage page)
{
    public ILocator Name => page.GetByTestId(SettingsTestIds.GeneralName);

    public ILocator TabRow => page.GetByTestId(SettingsTestIds.TabRow);

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

    public async Task OpenAsync(string tab = "general")
    {
        await page.GotoAsync(Links.ProjectSettings("SHIP", tab));
        await Assertions.Expect(TabRow).ToBeVisibleAsync();
    }

    public async Task<bool> PageFitsAsync() =>
        await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth");

    public static async Task<string> FontSizeAsync(ILocator element) =>
        await element.EvaluateAsync<string>("el => getComputedStyle(el).fontSize");
}
