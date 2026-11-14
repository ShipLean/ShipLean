using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.TaskChanges;

// The full-width Changes page: toolbar, files, diff and comments. Every action goes through test ids.
public sealed class ChangesPage(IPage page)
{
    public ILocator Screen => page.GetByTestId(TaskChangesTestIds.Screen);

    public ILocator NarrowNotice => page.GetByTestId(TaskChangesTestIds.NarrowNotice);

    public ILocator Toolbar => page.GetByTestId(TaskChangesTestIds.Toolbar);

    public ILocator CommitOptions => page.GetByTestId(TaskChangesTestIds.CommitOption);

    public ILocator NewCommitBanner => page.GetByTestId(TaskChangesTestIds.NewCommitBanner);

    public ILocator FileRows => page.GetByTestId(TaskChangesTestIds.FileRow);

    public ILocator FilesViewed => page.GetByTestId(TaskChangesTestIds.FilesViewed);

    public ILocator DiffFiles => page.GetByTestId(TaskChangesTestIds.DiffFile);

    public ILocator Markers => page.GetByTestId(TaskChangesTestIds.Marker);

    public ILocator OutdatedMarkers => page.GetByTestId(TaskChangesTestIds.MarkerOutdated);

    public ILocator Composer => page.GetByTestId(TaskChangesTestIds.Composer);

    public ILocator Threads => page.GetByTestId(TaskChangesTestIds.Thread);

    public ILocator ReviewError => page.GetByTestId(TaskChangesTestIds.ReviewError);

    public ILocator ApproveWarning => page.GetByTestId(TaskChangesTestIds.ApproveWarning);

    public async Task OpenAsync(string projectKey, string itemKey, string? commit = null, string? file = null, string? layout = null)
    {
        await page.GotoAsync(Links.TaskChanges(projectKey, itemKey, commit, file, layout));
        await Screen.WaitForAsync();
    }

    // Opens the narrow page, which shows the notice and fetches nothing.
    public async Task OpenNarrowAsync(string projectKey, string itemKey)
    {
        await page.GotoAsync(Links.TaskChanges(projectKey, itemKey));
        await NarrowNotice.WaitForAsync();
    }

    public Task BackFromNarrowNoticeAsync() => page.GetByTestId(TaskChangesTestIds.NarrowBack).ClickAsync();

    // Picks a commit in the picker; the page reads that commit's diff. Use PickAllCommitsAsync for "All commits".
    public async Task<IResponse> PickCommitAsync(Guid itemId, int index)
    {
        await page.GetByTestId(TaskChangesTestIds.CommitPicker).ClickAsync();
        return await page.RunAndWaitForApiAsync(
            $"/api/work-items/{itemId}/changes", "GET", () => CommitOptions.Nth(index).ClickAsync());
    }

    public async Task PickAllCommitsAsync()
    {
        await page.GetByTestId(TaskChangesTestIds.CommitPicker).ClickAsync();
        await CommitOptions.First.ClickAsync();
    }

    public Task ToggleLayoutAsync() => page.GetByTestId(TaskChangesTestIds.LayoutToggle).ClickAsync();

    public Task OpenFileAsync(int index) => FileRows.Nth(index).ClickAsync();

    // The Viewed box of the file row at index.
    public Task ToggleViewedAsync(int index) => FileRows.Nth(index).GetByTestId(TaskChangesTestIds.FileViewed).ClickAsync();

    // Hovers the diff line of a file and clicks its +; the composer opens under that line.
    public async Task AddLineCommentAsync(int fileIndex, int lineIndex)
    {
        var line = DiffFiles.Nth(fileIndex).GetByTestId(TaskChangesTestIds.DiffAddLine).Nth(lineIndex);
        await line.HoverAsync();
        await line.ClickAsync();
        await Composer.WaitForAsync();
    }

    // Writes the comment in the open composer and posts it; returns the POST response.
    public async Task<IResponse> PostCommentAsync(Guid itemId, string body)
    {
        await page.GetByTestId(TaskChangesTestIds.ComposerBody).FillAsync(body);
        return await page.RunAndWaitForApiAsync(
            $"/api/work-items/{itemId}/code-comments", "POST", () => page.GetByTestId(TaskChangesTestIds.ComposerPost).ClickAsync());
    }

    public Task CancelComposerAsync() => page.GetByTestId(TaskChangesTestIds.ComposerCancel).ClickAsync();

    public async Task<IResponse> ReplyAsync(Guid itemId, Guid commentId, int threadIndex, string body)
    {
        var thread = Threads.Nth(threadIndex);
        await thread.GetByTestId(TaskChangesTestIds.ReplyOpen).ClickAsync();
        await thread.GetByTestId(TaskChangesTestIds.ReplyBody).FillAsync(body);
        return await page.RunAndWaitForApiAsync(
            $"/api/work-items/{itemId}/code-comments/{commentId}/replies", "POST", () => thread.GetByTestId(TaskChangesTestIds.ReplySend).ClickAsync());
    }

    public Task<IResponse> ResolveAsync(Guid itemId, Guid commentId, int threadIndex) =>
        page.RunAndWaitForApiAsync(
            $"/api/work-items/{itemId}/code-comments/{commentId}/resolve", "POST",
            () => Threads.Nth(threadIndex).GetByTestId(TaskChangesTestIds.Resolve).ClickAsync());

    // Reloads after the banner said a teammate added a commit.
    public Task ReloadForNewCommitAsync() => page.GetByTestId(TaskChangesTestIds.NewCommitReload).ClickAsync();

    public async Task<IResponse> ApproveAsync(Guid itemId, string? note = null)
    {
        await page.GetByTestId(TaskChangesTestIds.Approve).ClickAsync();
        if (note is not null)
        {
            await page.GetByTestId(TaskChangesTestIds.ApproveNote).FillAsync(note);
        }
        return await page.RunAndWaitForApiAsync(
            $"/api/work-items/{itemId}/changes/approve", "POST", () => page.GetByTestId(TaskChangesTestIds.ApproveConfirm).ClickAsync());
    }

    public async Task<IResponse> RequestChangesAsync(Guid itemId, string? note = null, bool keepAssignee = false)
    {
        await page.GetByTestId(TaskChangesTestIds.RequestChanges).ClickAsync();
        if (note is not null)
        {
            await page.GetByTestId(TaskChangesTestIds.RequestNote).FillAsync(note);
        }
        if (keepAssignee)
        {
            await page.GetByTestId(TaskChangesTestIds.RequestKeepAssignee).CheckAsync();
        }
        return await page.RunAndWaitForApiAsync(
            $"/api/work-items/{itemId}/changes/request", "POST", () => page.GetByTestId(TaskChangesTestIds.RequestConfirm).ClickAsync());
    }
}
