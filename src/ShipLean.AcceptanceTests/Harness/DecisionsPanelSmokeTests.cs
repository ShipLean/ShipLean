using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Harness;

// Proves the Decisions page object and the seeding helper against the real app. J1a and J1b build on both.
[TestFixture]
public class DecisionsPanelSmokeTests : SeededAcceptanceTest
{
    [Test]
    public async Task DecisionsPanel_WhenItemHasNone_ShouldShowSection()
    {
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-147");
        var panel = new DecisionsPanel(Page, SeedIds.WorkItems.Ship147);

        await Expect(panel.Section).ToBeVisibleAsync();
        await Expect(panel.Empty).ToBeVisibleAsync();
    }

    [Test]
    public async Task DecisionsPanel_WhenAsksSeeded_ShouldShowRowStates()
    {
        var (itemId, key) = await AskSeeding.SeedItemAsync(ConnectionString);
        await AskSeeding.SeedDecisionsAsync(
            ConnectionString, itemId,
            new AskSeeding.DecisionSeed("Which store?", "Postgres"),
            new AskSeeding.DecisionSeed("Which queue?", "Redis", State: "confirmed"));
        await AskSeeding.SeedQuestionAsync(ConnectionString, itemId, "Which tenant?");
        await AskSeeding.SeedQuestionAsync(ConnectionString, itemId, "Which region?", "answered", "eu-west");
        await new WorkItemScreen(Page).OpenAsync("SHIP", key);
        var panel = new DecisionsPanel(Page, itemId);

        await Expect(panel.DecisionRow(1)).ToHaveAttributeAsync("data-state", "open");
        await Expect(panel.DecisionRow(2)).ToHaveAttributeAsync("data-state", "confirmed");
        await Expect(panel.QuestionStateChip(1)).ToHaveTextAsync(new Regex("Needs an answer"));
        await Expect(panel.QuestionStateChip(2)).ToHaveTextAsync(new Regex("Answered"));
        await Expect(panel.RailAskLine("D2", "confirmed")).ToBeVisibleAsync();
        await Expect(panel.RailAskLine("Q2", "answered")).ToBeVisibleAsync();
    }

    [Test]
    public async Task DecisionsPanel_WhenChildHasOpenAsk_ShouldShowRollUpRow()
    {
        var feature = await AskSeeding.SeedItemAsync(ConnectionString, "feature", "Arranged feature");
        var child = await AskSeeding.SeedItemAsync(ConnectionString, "story", "Arranged child", feature.Id);
        await AskSeeding.SeedQuestionAsync(ConnectionString, child.Id, "Which child tenant?");
        await new WorkItemScreen(Page).OpenAsync("SHIP", feature.Key);
        var panel = new DecisionsPanel(Page, feature.Id);

        await Expect(panel.RollUpRow(child.Key)).ToBeVisibleAsync();
    }
}
