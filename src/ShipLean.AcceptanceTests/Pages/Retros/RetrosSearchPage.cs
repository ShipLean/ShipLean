using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// Retrospectives › Search: the search box, the filters, one row per point and the selected retro.
public sealed class RetrosSearchPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(RetrosTestIds.RetrosSearchPanel);

    public ILocator SearchBox => page.GetByTestId(RetrosTestIds.RetrosSearchBox);

    public ILocator FilterSprint => page.GetByTestId(RetrosTestIds.RetrosFilterSprint);

    public ILocator FilterCategory => page.GetByTestId(RetrosTestIds.RetrosFilterCategory);

    public ILocator FilterTheme => page.GetByTestId(RetrosTestIds.RetrosFilterTheme);

    public ILocator FilterLevel => page.GetByTestId(RetrosTestIds.RetrosFilterLevel);

    public ILocator FilterRole => page.GetByTestId(RetrosTestIds.RetrosFilterRole);

    public ILocator FilterRecurring => page.GetByTestId(RetrosTestIds.RetrosFilterRecurring);

    public ILocator FilterDecision => page.GetByTestId(RetrosTestIds.RetrosFilterDecision);

    public ILocator FilterNoSprint => page.GetByTestId(RetrosTestIds.RetrosFilterNosprint);

    public ILocator FilterUnthemed => page.GetByTestId(RetrosTestIds.RetrosFilterUnthemed);

    public ILocator FilterClear => page.GetByTestId(RetrosTestIds.RetrosFilterClear);

    public ILocator Rows => page.GetByTestId(RetrosTestIds.RetrosRows);

    public ILocator Row => page.GetByTestId(RetrosTestIds.RetrosRow);

    public ILocator RowOf(string text) => Row.Filter(new() { HasText = text });

    public ILocator Empty => page.GetByTestId(RetrosTestIds.RetrosEmpty);

    public ILocator Selected => page.GetByTestId(RetrosTestIds.RetrosSelected);

    public ILocator SelectedCard => Selected.GetByTestId(RetroSharedTestIds.RetroCard);

    // Opens the search with the filters in the address (a null or empty filter is left out).
    public async Task OpenAsync(string projectKey, params (string Key, string? Value)[] filters)
    {
        await page.GotoAsync(Links.Retros(projectKey, filters));
        await Panel.WaitForAsync();
    }
}
