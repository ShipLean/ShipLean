using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature;

// A Markdown attachment on its own page: header, toolbar and rendered body.
public sealed class AttachmentPage(IPage page)
{
    public ILocator Screen => page.GetByTestId(AttachmentTestIds.Screen);

    public ILocator Name => page.GetByTestId(AttachmentTestIds.Name);

    public ILocator Meta => page.GetByTestId(AttachmentTestIds.Meta);

    public ILocator WorkItem => page.GetByTestId(AttachmentTestIds.WorkItem);

    public ILocator Body => page.GetByTestId(AttachmentTestIds.Body);

    public async Task OpenAsync(string projectKey, string itemKey, Guid attachmentId)
    {
        await page.GotoAsync($"{Links.WorkItem(projectKey, itemKey)}/attachments/{attachmentId}");
        await Name.WaitForAsync();
    }
}
