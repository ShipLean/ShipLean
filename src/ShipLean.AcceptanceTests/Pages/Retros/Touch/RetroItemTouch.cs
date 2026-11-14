using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros.Touch;

// One item's retros at 900px and below (SHIP-729): the header buttons, the one-row toolbar and the rail's sheets. Use with
// RetroItemPage for the panel, board, child rows and the action dialog.
public sealed class RetroItemTouch(IPage page)
{
    public ILocator Back => page.GetByTestId(TouchTestIds.TopBarBack);

    public ILocator KpiToDecide => page.GetByTestId(RetrosTestIds.RetroItemKpiToDecide);

    public ILocator KpiActions => page.GetByTestId(RetrosTestIds.RetroItemKpiActions);

    public ILocator KpiDecided => page.GetByTestId(RetrosTestIds.RetroItemKpiDecided);

    public ILocator RetroSelect => page.GetByTestId(RetrosTestIds.RetroItemRetroSelect);

    public ILocator More => page.GetByTestId(TouchTestIds.MoreButton);

    public ILocator MoreRow(string label) =>
        page.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasText = label });

    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator SheetClose => page.GetByTestId(TouchTestIds.SheetClose);

    public ILocator SheetWriters => page.GetByTestId(RetrosTestIds.RetroItemSheetWriters);

    public ILocator SheetActions => page.GetByTestId(RetrosTestIds.RetroItemSheetActions);

    public ILocator Columns => page.GetByTestId(RetroSharedTestIds.RetroBoardColumn);
}
