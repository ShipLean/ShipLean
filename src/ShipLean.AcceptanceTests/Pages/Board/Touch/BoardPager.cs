using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board.Touch;

// A level's board at 900px and below (SHIP-719): one column a page in the touch pager, column chips above.
public sealed class BoardPager(IPage page)
{
    public ILocator Grid => page.GetByTestId(BoardTestIds.PagerGrid);

    public ILocator ChipStrip => Grid.GetByTestId(TouchTestIds.PagerChips);

    public ILocator Chips => Grid.GetByTestId(TouchTestIds.PagerChip);

    public ILocator Track => Grid.GetByTestId(TouchTestIds.Pager);

    public ILocator Pages => Grid.GetByTestId(TouchTestIds.PagerPage);

    // A column's chip by the column's name (the chip also shows its count).
    public ILocator Chip(string columnName) =>
        Chips.Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^\\s*" + Regex.Escape(columnName) + "(?![A-Za-z])") });

    // The page at the same position as the chip.
    public ILocator PageAt(int index) => Pages.Nth(index);

    // Waits for the first column header: the board's data has rendered.
    public Task WaitAsync() => Grid.GetByTestId(BoardTestIds.ColumnHeader).First.WaitForAsync();
}
