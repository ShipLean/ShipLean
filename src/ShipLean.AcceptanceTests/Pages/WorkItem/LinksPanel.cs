using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem;

// The links part of the work item page: rows, and the form that adds one to the draft.
public sealed class LinksPanel(IPage page)
{
    public ILocator Rows => page.GetByTestId(WorkItemTestIds.LinkRow);

    public ILocator Count => page.GetByTestId(WorkItemTestIds.LinksCount);

    public async Task AddWorkItemLinkAsync(string type, string key)
    {
        await page.GetByTestId(WorkItemTestIds.LinkAdd).ClickAsync();
        await page.GetByTestId(WorkItemTestIds.LinkType).SelectOptionAsync(type);
        await page.GetByTestId(WorkItemTestIds.LinkKey).FillAsync(key);
        await page.GetByTestId(WorkItemTestIds.LinkSubmit).ClickAsync();
    }
}
