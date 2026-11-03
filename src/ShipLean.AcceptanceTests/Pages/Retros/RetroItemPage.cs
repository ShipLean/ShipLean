using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// One item's retro screen: the board of its notes, the rail and, on a Feature, a row per story and bug.
public sealed class RetroItemPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(RetrosTestIds.RetroItemPanel);

    public RetroBoardPart Board => new(Panel);

    public RetroActionDialogPart Dialog => new(page);

    public ILocator Filter => page.GetByTestId(RetrosTestIds.RetroItemFilter);

    public ILocator AddAction => page.GetByTestId(RetrosTestIds.RetroItemAddAction);

    public ILocator Rail => page.GetByTestId(RetrosTestIds.RetroItemRail);

    // The rail's "Who wrote this" block and the author chips in it.
    public ILocator Writers => page.GetByTestId(RetrosTestIds.RetroItemRailWriters);

    public ILocator WriterChips => Writers.GetByTestId(RetroSharedTestIds.RetroAuthorChip);

    // The rail's "Compiled from" block (a Feature's retro lists the stories and bugs it was compiled from).
    public ILocator Compiled => page.GetByTestId(RetrosTestIds.RetroItemRailCompiled);

    public ILocator RailActions => page.GetByTestId(RetrosTestIds.RetroItemRailActions);

    // The raw failure logs below the board, collapsed until a toggle opens them.
    public ILocator Logs => page.GetByTestId(RetrosTestIds.RetroItemLogs);

    public ILocator LogToggle => Logs.GetByTestId(RetroSharedTestIds.RetroCardLogToggle);

    public ILocator Log => Logs.GetByTestId(RetroSharedTestIds.RetroCardLog);

    public ILocator ChildRowsSection => page.GetByTestId(RetrosTestIds.RetroItemChildRows);

    public ILocator ChildRows => page.GetByTestId(RetrosTestIds.RetroItemChildRow);

    public ILocator ChildRowOf(string text) => ChildRows.Filter(new() { HasText = text });

    // Opens the item's retro screen; `parameters` are retro=, decide=open and point= (null or empty ones are left out).
    public async Task OpenAsync(string projectKey, string itemKey, params (string Key, string? Value)[] parameters)
    {
        await page.GotoAsync(Links.RetroItem(projectKey, itemKey, parameters));
        await Panel.WaitForAsync();
    }
}
