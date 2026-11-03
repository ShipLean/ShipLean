using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// Retrospectives › Insights: the view tabs and range, Recurring themes with What keeps working, Merge themes, the Action
// items table and the Changes & impact cards.
public sealed class RetroInsightsPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(RetrosTestIds.RetroInsightsPanel);

    public ILocator Tabs => page.GetByTestId(RetrosTestIds.RetroInsightsTab);

    public ILocator Tab(string label) => Tabs.Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(label)}$") });

    public ILocator Range => page.GetByTestId(RetrosTestIds.RetroInsightsRange);

    public ILocator RecurringTable => page.GetByTestId(RetrosTestIds.RetroRecurringTable);

    public ILocator RecurringRows => page.GetByTestId(RetrosTestIds.RetroRecurringRow);

    public ILocator RecurringRowOf(string theme) => RecurringRows.Filter(new() { HasText = theme });

    public ILocator Rename(string theme) => RecurringRowOf(theme).GetByTestId(RetrosTestIds.RetroThemeRename);

    public ILocator NoSprintHead => page.GetByTestId(RetrosTestIds.RetroInsightsNosprint);

    // One cell per column (a sprint or a week) in a theme's row.
    public ILocator MentionCellsOf(string theme) => RecurringRowOf(theme).GetByTestId(RetrosTestIds.RetroMentionCell);

    public ILocator TotalOf(string theme) => RecurringRowOf(theme).GetByTestId(RetrosTestIds.RetroTotalCell);

    public ILocator NoSprintOf(string theme) => RecurringRowOf(theme).GetByTestId(RetrosTestIds.RetroNosprintCell);

    public ILocator StatusOf(string theme) => RecurringRowOf(theme).GetByTestId(RetrosTestIds.RetroStatusChip);

    public ILocator Tooltip => page.GetByTestId(RetrosTestIds.RetroTipBubble);

    public ILocator Callout => page.GetByTestId(RetrosTestIds.RetroRecurringCallout);

    public ILocator UnthemedRow => page.GetByTestId(RetrosTestIds.RetroInsightsUnthemed);

    public ILocator GiveThemes => page.GetByTestId(RetrosTestIds.RetroUnthemedLink);

    public ILocator ThemeAction(string theme) => RecurringRowOf(theme).GetByTestId(RetrosTestIds.RetroInsightsThemeAction);

    public ILocator Proposals => page.GetByTestId(RetrosTestIds.RetroMergeProposal);

    public ILocator ProposalOf(string theme) => Proposals.Filter(new() { HasText = theme });

    public ILocator ProposalMerge(string theme) => ProposalOf(theme).GetByTestId(RetrosTestIds.RetroMergeProposalMerge);

    public ILocator ProposalKeep(string theme) => ProposalOf(theme).GetByTestId(RetrosTestIds.RetroMergeProposalKeep);

    public ILocator FixOf(string action) => ActionRowOf(action).GetByTestId(RetrosTestIds.RetroActionFixCell);

    public ILocator CoversOf(string action) => ActionRowOf(action).GetByTestId(RetrosTestIds.RetroActionCoversCell);

    public ILocator KeepsWorking => page.GetByTestId(RetrosTestIds.RetroKeepsWorking);

    public ILocator MergeThemes => page.GetByTestId(RetrosTestIds.RetroMergeThemes);

    public ILocator MergeDialog => page.GetByTestId(RetrosTestIds.RetroMergeDialog);

    public ILocator MergeSource => page.GetByTestId(RetrosTestIds.RetroMergeSource);

    public ILocator MergeTarget => page.GetByTestId(RetrosTestIds.RetroMergeTarget);

    public ILocator MergePreview => page.GetByTestId(RetrosTestIds.RetroMergePreview);

    public ILocator MergeSave => page.GetByTestId(RetrosTestIds.RetroMergeSave);

    public ILocator ActionsTable => page.GetByTestId(RetrosTestIds.RetroActionsTable);

    public ILocator ActionRows => page.GetByTestId(RetrosTestIds.RetroActionRow);

    public ILocator ActionRowOf(string text) => ActionRows.Filter(new() { HasText = text });

    public ILocator Pull(string text) => ActionRowOf(text).GetByTestId(RetrosTestIds.RetroActionPull);

    public ILocator ImpactCards => page.GetByTestId(RetrosTestIds.RetroImpactCard);

    public ILocator ImpactCardOf(string text) => ImpactCards.Filter(new() { HasText = text });

    // Opens the insights; view is themes, actions or impact and range 6, 3 or all (null or empty for the default).
    public async Task OpenAsync(string projectKey, string? view = null, string? range = null)
    {
        await page.GotoAsync(Links.RetroInsights(projectKey, ("view", view), ("range", range)));
        await Panel.WaitForAsync();
    }
}
