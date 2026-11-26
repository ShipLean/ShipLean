using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Prompts;

// The Ways of working page as a member, the Issue proposal journey and the phone tests read it.
public sealed class PromptsMemberPage(IPage page)
{
    public ILocator Root => page.GetByTestId(PromptsTestIds.Page);

    public ILocator ReadOnlyNote => page.GetByTestId(PromptsTestIds.ReadOnlyNote);

    public ILocator ReadOnlyAdmins => page.GetByTestId(PromptsTestIds.ReadOnlyAdmins);

    // The twelve built-in prompts as the strip counts them, Intake to Sprint demo.
    public static readonly string[] TwelvePromptsByStage = ["1 prompt", "1 prompt", "2 prompts", "3 prompts", "2 prompts", "2 prompts", "1 prompt"];

    // Each stage's "N prompts" count in the strip, in stage order.
    public ILocator StageCounts => page.GetByTestId(PromptsTestIds.StageStripCount);

    public ILocator StageNav => page.GetByTestId(PromptsTestIds.StageNav);

    public ILocator StageStrip => page.GetByTestId(PromptsTestIds.StageStrip);

    public ILocator Detail => page.GetByTestId(PromptsTestIds.Detail);

    public ILocator DetailName => page.GetByTestId(PromptsTestIds.DetailName);

    public ILocator DetailChip => page.GetByTestId(PromptsTestIds.DetailChip);

    public ILocator Actions => page.GetByTestId(PromptsTestIds.Actions);

    public ILocator Customize => page.GetByTestId(PromptsTestIds.ActionCustomize);

    public ILocator Edit => page.GetByTestId(PromptsTestIds.ActionEdit);

    public ILocator UseBuiltIn => page.GetByTestId(PromptsTestIds.ActionUseBuiltIn);

    public ILocator HistoryUse => page.GetByTestId(PromptsTestIds.HistoryUse);

    public ILocator HistoryBuiltInUse => page.GetByTestId(PromptsTestIds.HistoryBuiltInUse);

    public ILocator UpdateApply => page.GetByTestId(PromptsTestIds.UpdateApply);

    public ILocator UpdateKeep => page.GetByTestId(PromptsTestIds.UpdateKeep);

    public ILocator PanelHistory => page.GetByTestId(PromptsTestIds.PanelHistory);

    public ILocator HistoryRows => page.GetByTestId(PromptsTestIds.HistoryRow);

    public ILocator HistoryRowInUse => page.GetByTestId(PromptsTestIds.HistoryRowInUse);

    public ILocator UpdateReview => page.GetByTestId(PromptsTestIds.UpdateReview);

    public ILocator Resume => page.GetByTestId(PromptsTestIds.ActionResume);

    public ILocator HistoryNote => page.GetByTestId(PromptsTestIds.HistoryNote);

    public ILocator Tab(string testId) => page.GetByTestId(testId);

    public ILocator Panel(string testId) => page.GetByTestId(testId);

    public ILocator StageSelect => page.GetByTestId(PromptsTestIds.StageSelect);

    // A stage in the strip, by its name as shown.
    public ILocator Stage(string name) => page.GetByTestId(PromptsTestIds.Stage).Filter(new() { HasText = name });

    public async Task OpenAsync(string projectKey, string prompt)
    {
        await page.GotoAsync(Links.WaysOfWorking(projectKey, prompt));
        await Assertions.Expect(Detail).ToBeVisibleAsync();
    }
}
