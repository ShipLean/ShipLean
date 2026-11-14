using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board.Touch;

// A card's ⋯ on a phone and the sheets it opens (SHIP-719): the actions sheet, then Move to or Lane.
public sealed class CardActionsSheet(IPage page)
{
    private readonly TouchShell shell = new(page);

    public static ILocator More(ILocator card) => card.GetByTestId(TouchTestIds.MoreButton);

    public ILocator ActionsFor(string key) => shell.Sheet($"{key} actions");

    public ILocator MoveToSheet => shell.Sheet("Move to");

    public ILocator LaneSheet => shell.Sheet("Lane");

    // A row of a sheet by its label, a Move to row going on with " · WIP".
    public static ILocator Row(ILocator sheet, string startsWith) =>
        sheet.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^\\s*" + Regex.Escape(startsWith) + "(\\s·|\\s*$)") });

    // Taps ⋯ on the card, then Move to, then the column's row; returns the moves response.
    public Task<IResponse> MoveToColumnAsync(ILocator card, string key, string columnName) =>
        page.RunAndWaitForResponseAsync(
            async () =>
            {
                await More(card).TapAsync();
                await Row(ActionsFor(key), "Move to").TapAsync();
                await Row(MoveToSheet, columnName).TapAsync();
            },
            r => r.Request.Method == "POST" && Regex.IsMatch(new Uri(r.Url).AbsolutePath, "^/api/boards/[^/]+/moves$"));
}
