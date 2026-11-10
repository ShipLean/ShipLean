using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Harness;

// Proves the Decisions page object and the seeding helper against the real app. J1a and J1b build on both.
[TestFixture]
public class DecisionsPanelSmokeTests : SeededAcceptanceTest
{
    [Test, Order(1)]
    public async Task DecisionsPanel_WhenItemHasNone_ShouldShowSection()
    {
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-147");
        var panel = new DecisionsPanel(Page, SeedIds.WorkItems.Ship147);

        await Expect(panel.Section).ToBeVisibleAsync();
        await Expect(panel.Empty).ToBeVisibleAsync();
    }

    [Test, Order(2)]
    public async Task DecisionsPanel_WhenAsksSeeded_ShouldShowRowStates()
    {
        await AskSeeding.SeedDecisionsAsync(
            ConnectionString, SeedIds.WorkItems.Ship147, new AskSeeding.DecisionSeed("Which store?", "Postgres"));
        await AskSeeding.SeedQuestionAsync(ConnectionString, SeedIds.WorkItems.Ship147, "Which tenant?");
        await AskSeeding.SeedQuestionAsync(ConnectionString, SeedIds.WorkItems.Ship147, "Which region?", "answered", "eu-west");
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-147");
        var panel = new DecisionsPanel(Page, SeedIds.WorkItems.Ship147);

        await Expect(panel.DecisionRow(1)).ToBeVisibleAsync();
        (await panel.RowState(1)).ShouldBe("open");
        (await panel.QuestionState(1)).ShouldContain("Needs an answer");
        (await panel.QuestionState(2)).ShouldContain("Answered");
    }
}
