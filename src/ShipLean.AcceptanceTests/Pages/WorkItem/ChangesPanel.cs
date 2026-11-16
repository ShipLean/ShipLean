using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem;

// The Changes section of a Task or Bug page: commits, totals, the add-commit box and the Review changes link.
public sealed class ChangesPanel(IPage page)
{
    public ILocator Section => page.GetByTestId(TaskChangesTestIds.Section);

    public ILocator Rows => page.GetByTestId(TaskChangesTestIds.SectionRow);

    public ILocator Count => page.GetByTestId(TaskChangesTestIds.SectionCount);

    public ILocator Totals => page.GetByTestId(TaskChangesTestIds.SectionTotals);

    public ILocator OpenComments => page.GetByTestId(TaskChangesTestIds.SectionOpenComments);

    public ILocator Viewed => page.GetByTestId(TaskChangesTestIds.SectionViewed);

    public ILocator Empty => page.GetByTestId(TaskChangesTestIds.SectionEmpty);

    public ILocator AddError => page.GetByTestId(TaskChangesTestIds.SectionAddError);

    // Types the commit (a sha, or a commit URL with an empty repository) and submits; returns the POST response.
    public async Task<IResponse> AddCommitAsync(Guid itemId, string shaOrUrl)
    {
        await page.GetByTestId(TaskChangesTestIds.SectionAddOpen).ClickAsync();
        await page.GetByTestId(TaskChangesTestIds.SectionAddInput).FillAsync(shaOrUrl);
        return await page.RunAndWaitForApiAsync(
            $"/api/work-items/{itemId}/commits", "POST", () => page.GetByTestId(TaskChangesTestIds.SectionAddSubmit).ClickAsync());
    }

    // Follows Review changes to the Changes page.
    public Task ReviewAsync() => page.GetByTestId(TaskChangesTestIds.SectionReview).ClickAsync();
}
