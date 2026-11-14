using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros.Touch;

// Retrospectives › Search at 900px and below (SHIP-730): the filter button, the count, the chips, the Filter sheet and the
// three-line rows. Use with RetrosSearchPage for the panel, search box, rows and the Filter sheet's own controls.
public sealed class RetroSearchTouch(IPage page)
{
    public ILocator FilterOpen => page.GetByTestId(RetrosTestIds.RetrosFilterOpen);

    public ILocator Count => page.GetByTestId(RetrosTestIds.RetrosCount);

    public ILocator Chips => page.GetByTestId(RetrosTestIds.RetrosChip);

    public ILocator ChipsClear => page.GetByTestId(RetrosTestIds.RetrosChipsClear);

    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator Show => Sheet.GetByTestId(RetrosTestIds.RetrosFilterShow);

    public ILocator EmptyClear => page.GetByTestId(RetrosTestIds.RetrosEmptyClear);

    // A choice chip in the Filter sheet, by its label (e.g. "Went badly").
    public ILocator ChoiceOf(string label) =>
        Sheet.GetByTestId(RetrosTestIds.RetrosFilterChoice).Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(label)}$") });

    // An active filter's chip, by its label.
    public ILocator ChipOf(string label) => Chips.Filter(new LocatorFilterOptions { HasText = label });

    public static ILocator RootCauseOf(ILocator row) => row.GetByTestId(RetrosTestIds.RetrosRowRootCause);

    public async Task OpenSheetAsync()
    {
        await FilterOpen.TapAsync();
        await Assertions.Expect(Sheet).ToBeVisibleAsync();
    }
}
