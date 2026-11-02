using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// Retrospectives › Features: one row per Feature (and the loose stories and bugs), with a filter and a search box.
public sealed class RetroFeaturesPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(RetrosTestIds.RetroFeaturesPanel);

    public ILocator Rows => page.GetByTestId(RetrosTestIds.RetroFeaturesRow);

    public ILocator RowOf(string text) => Rows.Filter(new() { HasText = text });

    public ILocator Filter => page.GetByTestId(RetrosTestIds.RetroFeaturesFilter);

    public ILocator Search => page.GetByTestId(RetrosTestIds.RetroFeaturesSearch);

    public async Task OpenAsync(string projectKey)
    {
        await page.GotoAsync(Links.RetroFeatures(projectKey));
        await Panel.WaitForAsync();
    }
}
