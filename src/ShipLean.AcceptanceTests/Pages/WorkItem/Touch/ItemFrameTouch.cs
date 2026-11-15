using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem.Touch;

// The work item page's frame at 900px and below (SHIP-714): the section chips under the header and the type and key's
// copy button. The Save bar is the shell's (TouchShell), the conflict banner ConflictPanel.
public sealed class ItemFrameTouch(IPage page)
{
    public ILocator SectionChips => page.GetByTestId(WorkItemTestIds.SectionChips);

    public ILocator SectionChip(string label) =>
        page.GetByTestId(WorkItemTestIds.SectionChip).Filter(new LocatorFilterOptions { HasText = label });

    public ILocator CopyKey => page.GetByTestId(WorkItemTestIds.CopyKey);
}
