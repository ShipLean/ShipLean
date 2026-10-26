using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem;

// The fields strip's state and assignee cells.
public sealed class FieldsStrip(IPage page)
{
    public ILocator AssigneeButton => page.GetByTestId(WorkItemTestIds.AssigneeButton);

    public ILocator ParentOpen => page.GetByTestId(WorkItemTestIds.ParentOpen);

    public async Task PickStateAsync(string name)
    {
        await page.GetByTestId(WorkItemTestIds.StateButton).ClickAsync();
        await page.GetByTestId(ComponentTestIds.StateMenuOption).Filter(new() { HasText = name }).ClickAsync();
    }
}
