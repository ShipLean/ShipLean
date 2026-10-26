using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.ItemPeek;

// The work item modal. The item page has the same form test ids, so everything is scoped to the dialog.
public sealed class ItemPeekDialog(IPage page)
{
    public ILocator Dialog => page.GetByTestId(ItemPeekTestIds.Dialog);

    public ILocator Close => Dialog.GetByTestId(ItemPeekTestIds.Close);

    public ILocator OpenFullPage => Dialog.GetByTestId(ItemPeekTestIds.OpenFullPage);

    // The work item title field inside the dialog.
    public ILocator Title => Dialog.GetByTestId(WorkItemTestIds.Title);

    // Waits until the dialog shows this item (its form key), loaded.
    public async Task WaitOpenAsync(string itemKey)
    {
        await Dialog.GetByTestId(WorkItemTestIds.Key)
            .Filter(new LocatorFilterOptions { HasTextString = itemKey })
            .WaitForAsync();
    }

    public async Task WaitClosedAsync() => await Dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
}
