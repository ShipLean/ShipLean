using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board;

// The Definition of done popover a column header opens.
public sealed class BoardDod(IPage page)
{
    public ILocator Popover => page.GetByTestId(BoardTestIds.Dod);

    public ILocator Types => Popover.GetByTestId(BoardTestIds.DodType);

    public ILocator States => Popover.GetByTestId(BoardTestIds.DodState);

    public ILocator Body => Popover.GetByTestId(BoardTestIds.DodBody);

    public static ILocator Open(ILocator header) => header.GetByTestId(BoardTestIds.DodOpen);
}
