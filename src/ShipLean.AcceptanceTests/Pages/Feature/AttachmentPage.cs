using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature;

// A Markdown attachment on its own page: header, toolbar, rendered body and the editor.
public sealed class AttachmentPage(IPage page)
{
    public ILocator Screen => page.GetByTestId(AttachmentTestIds.Screen);

    public ILocator Name => page.GetByTestId(AttachmentTestIds.Name);

    public ILocator Meta => page.GetByTestId(AttachmentTestIds.Meta);

    public ILocator WorkItem => page.GetByTestId(AttachmentTestIds.WorkItem);

    public ILocator Body => page.GetByTestId(AttachmentTestIds.Body);

    public ILocator Edit => page.GetByTestId(AttachmentTestIds.Edit);

    public ILocator EditorInput => page.GetByTestId(AttachmentTestIds.EditorInput);

    public ILocator Save => page.GetByTestId(AttachmentTestIds.Save);

    public async Task OpenAsync(string projectKey, string itemKey, Guid attachmentId)
    {
        await page.GotoAsync($"{Links.WorkItem(projectKey, itemKey)}/attachments/{attachmentId}");
        await Name.WaitForAsync();
    }
}
