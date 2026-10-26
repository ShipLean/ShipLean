using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem;

// The banner a stale save shows: who changed what since the revision the form was based on, and the two ways out.
public sealed class ConflictPanel(IPage page)
{
    public ILocator Banner => page.GetByTestId(WorkItemTestIds.Conflict);

    public ILocator Summary => page.GetByTestId(WorkItemTestIds.ConflictSummary);

    public ILocator TheirChanges => page.GetByTestId(WorkItemTestIds.ConflictTheirChange);

    public ILocator AlsoMine => page.GetByTestId(WorkItemTestIds.ConflictAlsoMine);

    public ILocator ReloadTheirs => page.GetByTestId(WorkItemTestIds.ConflictReloadTheirs);

    public ILocator ReapplyMine => page.GetByTestId(WorkItemTestIds.ConflictReapplyMine);
}
