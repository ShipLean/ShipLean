using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// Retrospectives › Features: one row per Feature (and the loose stories and bugs), with a filter and a search box.
public sealed class RetroFeaturesPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(RetrosTestIds.RetroFeaturesPanel);

    public ILocator Rows => page.GetByTestId(RetrosTestIds.RetroFeaturesRow);

    public ILocator RowOf(string text) => Rows.Filter(new() { HasText = text });

    // The stories and bugs with no Feature sit under this heading row.
    public ILocator LooseHeading => page.GetByTestId(RetrosTestIds.RetroFeaturesLoose);

    // A row's count for one category, found by the category's place in the table (the order K3 fixes).
    public static ILocator CountOf(ILocator row, string category) =>
        row.GetByTestId(RetrosTestIds.RetroCategoryCount).Nth(Array.IndexOf(CategoryOrder, category));

    private static readonly string[] CategoryOrder = ["wentWell", "notInPlan", "wentBadly", "improve"];

    // One option of the All / To decide / No Feature retro yet filter.
    public ILocator FilterOption(string label) => Filter.GetByTestId(RetrosTestIds.RetroFeaturesFilterOption).Filter(new() { HasText = label });

    // A row's link to the item's retros.
    public static ILocator OpenLinkOf(ILocator row) => row.GetByTestId(RetrosTestIds.RetroFeaturesLink);

    public ILocator Filter => page.GetByTestId(RetrosTestIds.RetroFeaturesFilter);

    public ILocator Search => page.GetByTestId(RetrosTestIds.RetroFeaturesSearch);

    public async Task OpenAsync(string projectKey)
    {
        await page.GotoAsync(Links.RetroFeatures(projectKey));
        await Panel.WaitForAsync();
    }
}
