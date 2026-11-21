using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Prompts;

// A project's Prompts settings tab as an admin works it: the list, the detail with its tabs, the editor and its checks, the built-in update and History.
public sealed class PromptsAdminPage(IPage page)
{
    public ILocator Page => page.GetByTestId(PromptsTestIds.Page);

    public ILocator StageStripItems => page.GetByTestId(PromptsTestIds.StageStripItem);

    public ILocator ListStages => page.GetByTestId(PromptsTestIds.ListStage);

    public ILocator ListRows => page.GetByTestId(PromptsTestIds.ListRow);

    public ILocator DetailName => page.GetByTestId(PromptsTestIds.DetailName);

    public ILocator DetailChip => page.GetByTestId(PromptsTestIds.DetailChip);

    public ILocator Facts => page.GetByTestId(PromptsTestIds.Facts);

    public ILocator Customize => page.GetByTestId(PromptsTestIds.ActionCustomize);

    public ILocator Edit => page.GetByTestId(PromptsTestIds.ActionEdit);

    public ILocator UseBuiltIn => page.GetByTestId(PromptsTestIds.ActionUseBuiltIn);

    public ILocator UseBuiltInConfirm => page.GetByTestId(PromptsTestIds.UseBuiltInConfirm);

    public ILocator Editor => page.GetByTestId(PromptsTestIds.Editor);

    public ILocator EditorText => page.GetByTestId(PromptsTestIds.EditorText);

    public ILocator EditorNote => page.GetByTestId(PromptsTestIds.EditorNote);

    public ILocator Save => page.GetByTestId(PromptsTestIds.EditorSave);

    public ILocator SizeText => page.GetByTestId(PromptsTestIds.EditorSizeText);

    public ILocator Checks => page.GetByTestId(PromptsTestIds.Checks);

    public ILocator PlaceholderMissing => page.GetByTestId(PromptsTestIds.CheckPlaceholderMissing);

    public ILocator PutBack => page.GetByTestId(PromptsTestIds.CheckPutBack);

    public ILocator UnclosedTag => page.GetByTestId(PromptsTestIds.CheckUnclosedTag);

    public ILocator UnknownTag => page.GetByTestId(PromptsTestIds.CheckUnknownTag);

    public ILocator FixTag => page.GetByTestId(PromptsTestIds.CheckFixTag);

    public ILocator CheckSize => page.GetByTestId(PromptsTestIds.CheckSize);

    public ILocator TabHistory => page.GetByTestId(PromptsTestIds.TabHistory);

    public ILocator TabUpdate => page.GetByTestId(PromptsTestIds.TabUpdate);

    public ILocator TabAssembled => page.GetByTestId(PromptsTestIds.TabAssembled);

    // Every version row: the one in use has its own test id.
    public ILocator HistoryRows => page.GetByTestId(PromptsTestIds.HistoryRow).Or(page.GetByTestId(PromptsTestIds.HistoryRowInUse));

    public ILocator HistoryUse => page.GetByTestId(PromptsTestIds.HistoryUse);

    public ILocator HistoryNote => page.GetByTestId(PromptsTestIds.HistoryNote);

    public ILocator HistoryBuiltInRow => page.GetByTestId(PromptsTestIds.HistoryBuiltInRow);

    public ILocator UpdateBanner => page.GetByTestId(PromptsTestIds.UpdateBanner);

    public ILocator UpdateBannerText => page.GetByTestId(PromptsTestIds.UpdateBannerText);

    public ILocator UpdateReview => page.GetByTestId(PromptsTestIds.UpdateReview);

    public ILocator UpdateApply => page.GetByTestId(PromptsTestIds.UpdateApply);

    public ILocator UpdateApplyEdit => page.GetByTestId(PromptsTestIds.UpdateApplyEdit);

    public ILocator UpdateKeep => page.GetByTestId(PromptsTestIds.UpdateKeep);

    public ILocator UpdateDiff => page.GetByTestId(PromptsTestIds.UpdateDiff);

    public ILocator AppliesCleanly => page.GetByTestId(PromptsTestIds.UpdateAppliesCleanly);

    public ILocator AgentOptions => page.GetByTestId(PromptsTestIds.AgentOption);

    public ILocator AssembledLayers => page.GetByTestId(PromptsTestIds.AssembledLayer);

    public ILocator AssembledPlanned => page.GetByTestId(PromptsTestIds.AssembledLayerPlanned);

    public ILocator AssembledText => page.GetByTestId(PromptsTestIds.AssembledText);

    // Confirms Use vN. The confirm button has no test id and the id pair is frozen (reported: needs one, e.g. prompts-history-confirm), so it is
    // clicked as the button that follows the "Agents get vN from their next call." note.
    public async Task ConfirmUseVersionAsync()
    {
        await Assertions.Expect(HistoryNote).ToBeVisibleAsync();
        await HistoryNote.EvaluateAsync("el => el.parentElement.querySelector('button.btn-signal').click()");
    }

    // The preview's text as the source block holds it, without its line-number gutter (an empty line is drawn as one space).
    public Task<string> AssembledRawTextAsync() =>
        AssembledText.EvaluateAsync<string>("el => Array.from(el.children).map(row => row.children[1].textContent.replace(/^ $/, '')).join('\\n')");

    public async Task OpenAsync(string projectKey, string? prompt = null)
    {
        await page.GotoAsync(prompt is null ? Links.ProjectSettings(projectKey, "prompts") : Links.ProjectSettings(projectKey, "prompts", prompt));
        await Assertions.Expect(Page).ToBeVisibleAsync();
    }

    public ILocator AgentOption(string label) => AgentOptions.Filter(new LocatorFilterOptions { HasText = label });

    // Opens the editor on the selected prompt (Customize, or Edit once a version exists) with its text prefilled.
    public async Task OpenEditorAsync()
    {
        var action = Customize.Or(Edit);
        await action.ClickAsync();
        await Assertions.Expect(EditorText).ToBeVisibleAsync();
    }

    // Replaces the editor's whole text.
    public Task SetTextAsync(string text) => EditorText.FillAsync(text);

    // Appends a line after the editor's current text.
    public async Task AppendLineAsync(string line)
    {
        var current = await EditorText.InputValueAsync();
        await EditorText.FillAsync(current.TrimEnd('\n') + "\n\n" + line + "\n");
    }

    public async Task SaveWithNoteAsync(string note)
    {
        await EditorNote.FillAsync(note);
        await Assertions.Expect(Save).ToBeEnabledAsync();
        var response = await page.RunAndWaitForResponseAsync(
            () => Save.ClickAsync(),
            r => r.Url.Contains("/prompts/", StringComparison.Ordinal) && r.Url.EndsWith("/versions", StringComparison.Ordinal) && r.Request.Method == "POST");
        response.Ok.ShouldBeTrue(await response.TextAsync());
    }

    // Customize, append one project rule and save it with a note.
    public async Task CustomizeAsync(string line, string note)
    {
        await OpenEditorAsync();
        await AppendLineAsync(line);
        await SaveWithNoteAsync(note);
    }

    // What an agent is served by planning_guide for the project, the structured markdown field (the text block has a summary line first).
    public static async Task<string> ServedGuideAsync(Uri endpoint, string token, string project, Implementation? clientInfo = null)
    {
        await using var mcp = clientInfo is null
            ? await McpTestClient.ConnectAsync(endpoint, token)
            : await McpTestClient.ConnectAsync(endpoint, token, clientInfo);
        var result = await mcp.CallToolAsync("planning_guide", new Dictionary<string, object?> { ["project"] = project });
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value.GetProperty("markdown").GetString()!;
    }
}
