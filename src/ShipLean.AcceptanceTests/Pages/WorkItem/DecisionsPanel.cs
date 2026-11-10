using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem;

// The Decisions and questions section of the work item page. Rows are picked by their label text (D1, Q2). Posting
// actions return the POST response, as DiscussionPanel.PostAsync does.
public sealed class DecisionsPanel(IPage page, Guid itemId)
{
    private string DecisionsPath => $"/api/work-items/{itemId}/decisions";

    private string QuestionsPath => $"/api/work-items/{itemId}/questions";

    // Section frame and header
    public ILocator Section => page.GetByTestId(AsksTestIds.Section);

    public ILocator Loading => page.GetByTestId(AsksTestIds.Loading);

    public ILocator LoadFailed => page.GetByTestId(AsksTestIds.LoadFailed);

    public ILocator Header => page.GetByTestId(AsksTestIds.Header);

    public ILocator Count => page.GetByTestId(AsksTestIds.Count);

    public ILocator WaitingOn => page.GetByTestId(AsksTestIds.WaitingOn);

    public ILocator Summary => page.GetByTestId(AsksTestIds.Summary);

    public ILocator FoldToggle => page.GetByTestId(AsksTestIds.FoldToggle);

    public ILocator Empty => page.GetByTestId(AsksTestIds.Empty);

    public ILocator BulkConfirm => page.GetByTestId(AsksTestIds.BulkConfirm);

    // Rows
    public ILocator DecisionRows => page.GetByTestId(AsksTestIds.DecisionRow);

    public ILocator QuestionRows => page.GetByTestId(AsksTestIds.QuestionRow);

    public ILocator DecisionRow(int n) =>
        DecisionRows.Filter(new() { Has = page.GetByTestId(AsksTestIds.DecisionLabel).GetByText($"D{n}", new() { Exact = true }) });

    public ILocator QuestionRow(int q) =>
        QuestionRows.Filter(new() { Has = page.GetByTestId(AsksTestIds.QuestionLabel).GetByText($"Q{q}", new() { Exact = true }) });

    // Inside one decision row
    public ILocator Answer(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionAnswer);

    public ILocator Tag(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionTag);

    public ILocator StateLine(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionState);

    public ILocator ConfirmButton(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionConfirm);

    public ILocator ChangeButton(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionChangeButton);

    public ILocator DiscussButton(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionDiscussButton);

    public ILocator MoreButton(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionMore);

    public ILocator WithdrawItem(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionWithdraw);

    public ILocator CopyLinkItem(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionCopyLink);

    public ILocator HistoryToggle(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionHistoryToggle);

    public ILocator History(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionHistory);

    // Change form
    public ILocator ChangeForm(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionChange);

    public ILocator ChangeOptions(int n) => ChangeForm(n).GetByTestId(AsksTestIds.ChangeOption);

    public ILocator ChangeOwnInput(int n) => ChangeForm(n).GetByTestId(AsksTestIds.ChangeOwnInput);

    public ILocator ChangeNote(int n) => ChangeForm(n).GetByTestId(AsksTestIds.ChangeNote);

    public ILocator ChangeSave(int n) => ChangeForm(n).GetByTestId(AsksTestIds.ChangeSave);

    public ILocator ChangeCancel(int n) => ChangeForm(n).GetByTestId(AsksTestIds.ChangeCancel);

    public ILocator ChangeSettledWarning(int n) => ChangeForm(n).GetByTestId(AsksTestIds.ChangeSettledWarning);

    public ILocator ChangeError(int n) => ChangeForm(n).GetByTestId(AsksTestIds.ChangeError);

    // Discuss form
    public ILocator DiscussForm(int n) => DecisionRow(n).GetByTestId(AsksTestIds.DecisionDiscuss);

    public ILocator DiscussInput(int n) => DiscussForm(n).GetByTestId(AsksTestIds.DiscussInput);

    public ILocator DiscussPost(int n) => DiscussForm(n).GetByTestId(AsksTestIds.DiscussPost);

    public ILocator DiscussCancel(int n) => DiscussForm(n).GetByTestId(AsksTestIds.DiscussCancel);

    public ILocator DiscussError(int n) => DiscussForm(n).GetByTestId(AsksTestIds.DiscussError);

    // Inside one question row
    public ILocator QuestionStateChip(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionState);

    public ILocator ReplyButton(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionReply);

    public ILocator ReplyInput(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionReplyInput);

    public ILocator ReplyPost(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionReplyPost);

    public ILocator ReplyCancel(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionReplyCancel);

    public ILocator ReplyError(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionReplyError);

    public ILocator QuestionMore(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionMore);

    public ILocator QuestionWithdraw(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionWithdraw);

    public ILocator QuestionWithdrawError(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionWithdrawError);

    public ILocator QuestionAnswer(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionAnswer);

    public ILocator QuestionAnswerLink(int q) => QuestionRow(q).GetByTestId(AsksTestIds.QuestionAnswerLink);

    // Feature roll-up, History rail and Discussion
    public ILocator RollUpRows => page.GetByTestId(AsksTestIds.WaitingChildRow);

    public ILocator RailAskLines => page.GetByTestId(AsksTestIds.HistoryLine);

    public ILocator CommentAskTags => page.GetByTestId(AsksTestIds.CommentAskTag);

    // Reads the row's data-state: open, confirmed, changed or withdrawn.
    public Task<string?> RowState(int n) => DecisionRow(n).GetAttributeAsync("data-state");

    // Reads the chip text: "Needs an answer", "✓ Answered" or "Withdrawn".
    public Task<string> QuestionState(int q) => QuestionStateChip(q).InnerTextAsync();

    // Opens the section when it is folded.
    public async Task ExpandAsync()
    {
        if (await FoldToggle.GetAttributeAsync("aria-expanded") == "false") await FoldToggle.ClickAsync();
    }

    // Clicks Confirm on an open decision; returns the POST .../decisions/confirm response.
    public Task<IResponse> ConfirmAsync(int n) =>
        page.RunAndWaitForApiAsync($"{DecisionsPath}/confirm", "POST", () => ConfirmButton(n).ClickAsync());

    // Clicks the bulk "Confirm N recommended" button; returns the same response.
    public Task<IResponse> ConfirmAllAsync() =>
        page.RunAndWaitForApiAsync($"{DecisionsPath}/confirm", "POST", () => BulkConfirm.ClickAsync());

    // Opens the change form, picks the option at index (recommended, alternatives..., [current], own last), optionally
    // writes a note, saves. Returns the POST response (.../confirm or .../{n}/change).
    public async Task<IResponse> ChangeAsync(int n, int option, string? note = null)
    {
        await ChangeButton(n).ClickAsync();
        await ChangeOptions(n).Nth(option).CheckAsync();
        return await SaveChangeAsync(n, note);
    }

    // Opens the change form, writes an own answer, optionally a note, saves.
    public async Task<IResponse> ChangeAsync(int n, string ownText, string? note = null)
    {
        await ChangeButton(n).ClickAsync();
        await ChangeOptions(n).Last.CheckAsync();
        await ChangeOwnInput(n).FillAsync(ownText);
        return await SaveChangeAsync(n, note);
    }

    // Opens the discuss form and posts; returns the POST .../{n}/discuss response.
    public async Task<IResponse> DiscussAsync(int n, string text)
    {
        await DiscussButton(n).ClickAsync();
        await DiscussInput(n).FillAsync(text);
        return await page.RunAndWaitForApiAsync($"{DecisionsPath}/{n}/discuss", "POST", () => DiscussPost(n).ClickAsync());
    }

    // Decision row menu, Withdraw D<n>; returns the POST .../{n}/withdraw response.
    public async Task<IResponse> WithdrawAsync(int n)
    {
        await MoreButton(n).ClickAsync();
        return await page.RunAndWaitForApiAsync($"{DecisionsPath}/{n}/withdraw", "POST", () => WithdrawItem(n).ClickAsync());
    }

    // Question row menu, Withdraw Q<q>; returns the POST .../{q}/withdraw response.
    public async Task<IResponse> WithdrawQuestionAsync(int q)
    {
        await QuestionMore(q).ClickAsync();
        return await page.RunAndWaitForApiAsync($"{QuestionsPath}/{q}/withdraw", "POST", () => QuestionWithdraw(q).ClickAsync());
    }

    // Clicks Reply, types the answer, posts; returns the POST .../{q}/answer response.
    public async Task<IResponse> ReplyAsync(int q, string text)
    {
        await ReplyButton(q).ClickAsync();
        await ReplyInput(q).FillAsync(text);
        return await page.RunAndWaitForApiAsync($"{QuestionsPath}/{q}/answer", "POST", () => ReplyPost(q).ClickAsync());
    }

    private async Task<IResponse> SaveChangeAsync(int n, string? note)
    {
        if (note is not null) await ChangeNote(n).FillAsync(note);
        // Recommended with no note on an open decision posts to confirm; every other save posts to {n}/change.
        return await page.RunAndWaitForResponseAsync(
            () => ChangeSave(n).ClickAsync(),
            r => r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath is var path
                && (path == $"{DecisionsPath}/{n}/change" || path == $"{DecisionsPath}/confirm"));
    }
}
