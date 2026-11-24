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

    public ILocator ListStages => page.GetByTestId(PromptsTestIds.ListStage);

    public ILocator ListRows => page.GetByTestId(PromptsTestIds.ListRow);

    public ILocator DetailName => page.GetByTestId(PromptsTestIds.DetailName);

    public ILocator DetailChip => page.GetByTestId(PromptsTestIds.DetailChip);

    public ILocator Facts => page.GetByTestId(PromptsTestIds.Facts);

    public ILocator Customize => page.GetByTestId(PromptsTestIds.ActionCustomize);

    public ILocator Edit => page.GetByTestId(PromptsTestIds.ActionEdit);

    public ILocator UseBuiltIn => page.GetByTestId(PromptsTestIds.ActionUseBuiltIn);

    public ILocator UseBuiltInConfirm => page.GetByTestId(PromptsTestIds.UseBuiltInConfirm);

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

    public ILocator TabHistory => page.GetByTestId(PromptsTestIds.TabHistory);

    public ILocator TabAssembled => page.GetByTestId(PromptsTestIds.TabAssembled);

    // Every version row: the one in use has its own test id.
    public ILocator HistoryRows => page.GetByTestId(PromptsTestIds.HistoryRow).Or(page.GetByTestId(PromptsTestIds.HistoryRowInUse));

    // The "via <agent>" line on a version an agent saved.
    public ILocator HistoryAgent => page.GetByTestId(PromptsTestIds.HistoryAgent);

    // The "via <agent>" line inside one version's row.
    public static ILocator HistoryAgentIn(ILocator row) => row.GetByTestId(PromptsTestIds.HistoryAgent);

    // Shown in place of the table when a prompt has no saved versions.
    public ILocator HistoryEmpty => page.GetByTestId(PromptsTestIds.HistoryEmpty);

    public ILocator HistoryUse => page.GetByTestId(PromptsTestIds.HistoryUse);

    public ILocator UpdateBanner => page.GetByTestId(PromptsTestIds.UpdateBanner);

    public ILocator UpdateBannerText => page.GetByTestId(PromptsTestIds.UpdateBannerText);

    public ILocator UpdateReview => page.GetByTestId(PromptsTestIds.UpdateReview);

    public ILocator UpdateApply => page.GetByTestId(PromptsTestIds.UpdateApply);

    public ILocator UpdateApplyEdit => page.GetByTestId(PromptsTestIds.UpdateApplyEdit);

    public ILocator UpdateKeep => page.GetByTestId(PromptsTestIds.UpdateKeep);

    public ILocator UpdateDiff => page.GetByTestId(PromptsTestIds.UpdateDiff);

    public ILocator AppliesCleanly => page.GetByTestId(PromptsTestIds.UpdateAppliesCleanly);

    public ILocator AgentOptions => page.GetByTestId(PromptsTestIds.AgentOption);

    // Every layer row in the order drawn: the prompt's own layer, the planned ones, and the rest have different ids.
    public ILocator AssembledLayerRows => page.GetByTestId(PromptsTestIds.AssembledLayer)
        .Or(page.GetByTestId(PromptsTestIds.AssembledLayerThis)).Or(page.GetByTestId(PromptsTestIds.AssembledLayerPlanned));

    public ILocator AssembledPlanned => page.GetByTestId(PromptsTestIds.AssembledLayerPlanned);

    public ILocator AssembledText => page.GetByTestId(PromptsTestIds.AssembledText);

    public ILocator HistoryConfirm => page.GetByTestId(PromptsTestIds.HistoryConfirm);

    // The preview's text as the source block holds it, without its line-number gutter (an empty line is drawn as one space).
    public Task<string> AssembledRawTextAsync() =>
        AssembledText.EvaluateAsync<string>("el => Array.from(el.children).map(row => row.children[1].textContent.replace(/^ $/, '')).join('\\n')");

    // Picks an agent in the preview; waits for that agent's response.
    // Opens What the agent gets: the tab click fetches the preselected agent's text, so the wait wraps it.
    public async Task OpenAssembledAsync(string agentId = "claude-code")
    {
        await page.RunAndWaitForResponseAsync(
            () => TabAssembled.ClickAsync(),
            r => r.Url.Contains("/assembled?agent=" + agentId, StringComparison.Ordinal) && r.Ok);
        await Assertions.Expect(AgentOption("Claude Code")).ToHaveAttributeAsync("aria-pressed", "true");
    }

    public async Task ChooseAgentAsync(string label, string agentId)
    {
        await page.RunAndWaitForResponseAsync(
            () => AgentOption(label).ClickAsync(),
            r => r.Url.Contains("/assembled?agent=" + agentId, StringComparison.Ordinal) && r.Ok);
    }

    public async Task OpenAsync(string projectKey, string? prompt = null)
    {
        await page.GotoAsync(Links.WaysOfWorking(projectKey, prompt));
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
        var result = await mcp.CallToolAsync("planning_guide", new Dictionary<string, object?> { ["project"] = project, ["part"] = "guide" });
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value.GetProperty("markdown").GetString()!;
    }
}
