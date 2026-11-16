using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board.Touch;

// A card's ⋯ on a phone and the sheets it opens (SHIP-719): the actions sheet, whose Move to and Lane are rows of its own.
public sealed class CardActionsSheet(IPage page)
{
    private readonly TouchShell shell = new(page);

    public static ILocator More(ILocator card) => card.GetByTestId(TouchTestIds.MoreButton);

    public ILocator ActionsFor(string key) => shell.Sheet($"{key} actions");

    // The Assign sheet on Stories, the Owner sheet on Features and Epics, titled with the card's key (SHIP-719 L719e).
    public ILocator AssignSheet(string title, string key) => shell.Sheet($"{title} {key}");

    // A person's row of the Assign / Owner sheet by name ("Unassigned" or "No owner" for nobody).
    public static ILocator Person(ILocator sheet, string name) =>
        sheet.GetByTestId(BoardTestIds.AssignOption).Filter(new LocatorFilterOptions { HasTextRegex = new Regex(Regex.Escape(name) + "(\\s*\\(you\\))?\\s*$") });

    // A row of a sheet by its label; a row's value (WIP, who, here) follows the label, so the label is only a prefix.
    public static ILocator Row(ILocator sheet, string startsWith) =>
        sheet.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^\\s*" + Regex.Escape(startsWith)) });

    // Taps ⋯ on the card, then its Assign (Owner) row; Row anchors the label, so Move to top and bottom never match.
    public async Task OpenAssignAsync(ILocator card, string key, string label)
    {
        await More(card).TapAsync();
        await Row(ActionsFor(key), label).TapAsync();
    }

    // Taps ⋯ on the card, then the column's row of its Move to group; returns the moves response.
    public Task<IResponse> MoveToColumnAsync(ILocator card, string key, string columnName) =>
        page.RunAndWaitForResponseAsync(
            async () =>
            {
                await More(card).TapAsync();
                await Row(ActionsFor(key), columnName).TapAsync();
            },
            r => r.Request.Method == "POST" && Regex.IsMatch(new Uri(r.Url).AbsolutePath, "^/api/boards/[^/]+/moves$"));
}
