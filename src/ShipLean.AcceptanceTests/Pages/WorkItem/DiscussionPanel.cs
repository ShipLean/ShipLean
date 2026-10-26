using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem;

// The discussion part of the work item page: the comments, the new-comment box and its @ suggestions.
public sealed class DiscussionPanel(IPage page)
{
    public ILocator Comments => page.GetByTestId(WorkItemTestIds.Comment);

    public ILocator Count => page.GetByTestId(WorkItemTestIds.CommentsCount);

    public ILocator Input => page.GetByTestId(WorkItemTestIds.CommentInput);

    public ILocator Post => page.GetByTestId(WorkItemTestIds.CommentPost);

    public ILocator MentionMenu => page.GetByTestId(WorkItemTestIds.MentionMenu);

    public ILocator MentionOptions => page.GetByTestId(WorkItemTestIds.MentionOption);

    public ILocator ArchiveButtons => page.GetByTestId(WorkItemTestIds.CommentArchive);

    public ILocator Superseded => page.GetByTestId(WorkItemTestIds.CommentSuperseded);

    public ILocator Bodies => page.GetByTestId(WorkItemTestIds.CommentBody);

    public ILocator Via => page.GetByTestId(WorkItemTestIds.CommentVia);

    // Types the comment and clicks Comment; returns the POST response for the item's comments.
    public async Task<IResponse> PostAsync(Guid itemId, string body)
    {
        await Input.FillAsync(body);
        return await page.RunAndWaitForApiAsync($"/api/work-items/{itemId}/comments", "POST", () => Post.ClickAsync());
    }
}
