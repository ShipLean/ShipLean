using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem;

// The side rail of the work item page: attachments, history and Imported from.
public sealed class SideRailPanel(IPage page)
{
    public ILocator Attachments => page.GetByTestId(WorkItemTestIds.Attachment);

    public ILocator AttachmentCount => page.GetByTestId(WorkItemTestIds.AttachmentsCount);

    public ILocator Downloads => page.GetByTestId(WorkItemTestIds.AttachmentDownload);

    public ILocator ArchiveButtons => page.GetByTestId(WorkItemTestIds.AttachmentArchive);

    public ILocator FileInput => page.GetByTestId(WorkItemTestIds.AttachmentFile);

    public ILocator UploadError => page.GetByTestId(WorkItemTestIds.AttachmentError);

    public ILocator Revisions => page.GetByTestId(WorkItemTestIds.Revision);

    public ILocator ImportedFrom => page.GetByTestId(WorkItemTestIds.ImportedFrom);

    public ILocator ImportedFromLink => page.GetByTestId(WorkItemTestIds.ImportedFromLink);

    // Chooses a file and returns the POST response for the item's attachments.
    public Task<IResponse> UploadAsync(Guid itemId, string name, byte[] bytes, string mimeType = "application/octet-stream") =>
        page.RunAndWaitForApiAsync($"/api/work-items/{itemId}/attachments", "POST",
            () => FileInput.SetInputFilesAsync(new FilePayload { Name = name, MimeType = mimeType, Buffer = bytes }));
}
