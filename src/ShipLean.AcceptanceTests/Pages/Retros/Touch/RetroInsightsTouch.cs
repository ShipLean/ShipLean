using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros.Touch;

// Retrospective insights at 900px and below (SHIP-731): the tab row, Range ▾ and ⋯, and the Range, More and What these
// mean sheets. This is the lane's page object; L731b-d write their parts in files beside it. Use with RetroInsightsPage
// for the panel and the desktop parts.
public sealed class RetroInsightsTouch(IPage page)
{
    public ILocator Toolbar => page.GetByTestId(RetrosTestIds.RetroInsightsToolbar);

    public ILocator Tabs => page.GetByTestId(RetrosTestIds.RetroInsightsTab);

    public ILocator Tab(string label) => Tabs.Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(label)}$") });

    public ILocator RangeButton => page.GetByTestId(RetrosTestIds.RetroInsightsRange);

    public ILocator More => page.GetByTestId(TouchTestIds.MoreButton);

    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator SheetTitle => page.GetByTestId(TouchTestIds.SheetTitle);

    public ILocator SheetClose => page.GetByTestId(TouchTestIds.SheetClose);

    public ILocator MoreRows => Sheet.GetByTestId(TouchTestIds.ActionSheetRow);

    public ILocator MoreRow(string label) => MoreRows.Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(label)}") });

    public ILocator RangeOptions => Sheet.GetByTestId(RetrosTestIds.RetroInsightsRangeOption);

    public ILocator RangeOption(string label) => RangeOptions.Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^.?{Regex.Escape(label)}$") });

    public ILocator RangeGroups => Sheet.GetByTestId(RetrosTestIds.RetroInsightsRangeGroup);

    public ILocator DefsEntries => Sheet.GetByTestId(RetrosTestIds.RetroInsightsDefsEntry);

    public ILocator DefsEntryOf(string label) => DefsEntries.Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(label)}") });

    // How many entries the sheet highlights: one when it was opened from a chip, none from ⋯ or a heading.
    public Task<int> HighlightedCountAsync() =>
        DefsEntries.EvaluateAllAsync<int>("entries => entries.filter(e => e.getAttribute('data-highlighted') === 'true').length");
}
