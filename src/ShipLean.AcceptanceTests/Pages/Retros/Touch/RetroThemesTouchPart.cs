using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros.Touch;

// Recurring themes at 900px and below (SHIP-731 D1, D4, D9): the theme cards with their heat strips, the theme sheet and
// its Rename form. Use with RetroInsightsPage for the panel; the desktop table is its RecurringRowOf.
public sealed class RetroThemesTouchPart(IPage page)
{
    public ILocator Cards => page.GetByTestId(RetrosTestIds.RetroThemeCards);

    public ILocator Axis => page.GetByTestId(RetrosTestIds.RetroThemeAxis);

    public ILocator CardList => page.GetByTestId(RetrosTestIds.RetroThemeCard);

    public ILocator CardOf(string theme) => CardList.Filter(new() { HasText = theme });

    // The theme's name on its card, which opens the sheet like the rest of the card.
    public ILocator OpenOf(string theme) => CardOf(theme).GetByTestId(RetrosTestIds.RetroThemeCardOpen);

    public static ILocator HeatCellsOf(ILocator card) => card.GetByTestId(RetrosTestIds.RetroHeatCell);

    public static ILocator HeatTotalOf(ILocator card) => card.GetByTestId(RetrosTestIds.RetroHeatTotal);

    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator SheetTitle => page.GetByTestId(TouchTestIds.SheetTitle);

    public ILocator SheetClose => page.GetByTestId(TouchTestIds.SheetClose);

    public ILocator SheetCounts => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetCount);

    // Each shown column's count as "<column key>=<count>" (a sprint's key is its id), in column order.
    public async Task<string[]> SheetCountPairsAsync() =>
        await SheetCounts.EvaluateAllAsync<string[]>("cells => cells.map(c => c.getAttribute('data-column-key') + '=' + c.getAttribute('data-count'))");

    public ILocator SheetNoSprint => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetNoSprint);

    public ILocator SheetTotal => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetTotal);

    public ILocator SheetActions => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetAction);

    public ILocator SheetAddAction => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetAddAction);

    public ILocator SheetMentions => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetMention);

    public ILocator AllInSearch => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetAllInSearch);

    public ILocator RenameOpen => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetRename);

    public ILocator MergeInto => Sheet.GetByTestId(RetrosTestIds.RetroThemeSheetMerge);

    public ILocator RenameField => Sheet.GetByTestId(RetrosTestIds.RetroThemeRenameField);

    public ILocator RenameSave => Sheet.GetByTestId(RetrosTestIds.RetroThemeRenameSave);

    public ILocator RenameDiscard => Sheet.GetByTestId(RetrosTestIds.RetroThemeRenameDiscard);

    // The theme id the address asks the sheet to show (sheet=theme:<id>, the colon encoded or not), or null.
    public string? SheetThemeId => Regex.Match(page.Url, "[?&]sheet=theme(?:%3A|:)([^&]+)") is { Success: true } match ? match.Groups[1].Value : null;
}
