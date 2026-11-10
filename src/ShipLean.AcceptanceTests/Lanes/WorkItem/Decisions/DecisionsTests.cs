using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Decisions;

// The Decisions and questions section against the real app, signed in as Joel. Every test seeds its own item.
[TestFixture]
public class DecisionsTests : SeededAcceptanceTest
{
    private async Task<(Guid Id, string Key)> SeedOwnedItemAsync() =>
        await AskSeeding.SeedItemAsync(ConnectionString, assignee: SeedIds.Users.Joel);

    [Test]
    public async Task Decisions_WhenTwoOpen_ShouldConfirmBothFromHeader()
    {
        var (id, key) = await SeedOwnedItemAsync();
        await AskSeeding.SeedDecisionsAsync(
            ConnectionString, id,
            new AskSeeding.DecisionSeed("Which store?", "Postgres"),
            new AskSeeding.DecisionSeed("Which queue?", "Redis"));
        await new WorkItemScreen(Page).OpenAsync("SHIP", key);
        var panel = new DecisionsPanel(Page, id);
        await Expect(panel.BulkConfirm).ToBeVisibleAsync();

        var response = await panel.ConfirmAllAsync();

        response.Ok.ShouldBeTrue();
        await Expect(panel.Toasts.Filter(new() { HasText = "2 decisions confirmed as recommended" })).ToBeVisibleAsync();
        await Expect(panel.FoldToggle).ToHaveAttributeAsync("aria-expanded", "false");
        await Page.ReloadAsync();
        await Expect(panel.FoldToggle).ToBeVisibleAsync();
        await panel.ExpandAsync();
        await Expect(panel.DecisionRow(1)).ToHaveAttributeAsync("data-state", "confirmed");
        await Expect(panel.DecisionRow(2)).ToHaveAttributeAsync("data-state", "confirmed");
        await Expect(panel.Summary).ToContainTextAsync("2 decisions: 2 confirmed");
    }

    [Test]
    public async Task Decisions_WhenWithdrawn_ShouldStayVisibleStruckThrough()
    {
        var (id, key) = await SeedOwnedItemAsync();
        await AskSeeding.SeedDecisionsAsync(
            ConnectionString, id,
            new AskSeeding.DecisionSeed("Which store?", "Postgres"),
            new AskSeeding.DecisionSeed("Which queue?", "Redis"));
        await new WorkItemScreen(Page).OpenAsync("SHIP", key);
        var panel = new DecisionsPanel(Page, id);
        await Expect(panel.DecisionRow(1)).ToBeVisibleAsync();

        var response = await panel.WithdrawAsync(1);

        response.Ok.ShouldBeTrue();
        await Expect(panel.Toasts.Filter(new() { HasText = "D1 withdrawn. It stays listed." })).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Expect(panel.DecisionRow(1)).ToHaveAttributeAsync("data-state", "withdrawn");
        await Expect(panel.Answer(1)).ToHaveCSSAsync("text-decoration-line", "line-through");
    }

    [Test]
    public async Task Decisions_WhenFeatureChildWaits_ShouldListChildInRollUp()
    {
        var feature = await AskSeeding.SeedItemAsync(ConnectionString, "feature", "Arranged feature");
        var child = await AskSeeding.SeedItemAsync(ConnectionString, "story", "Arranged child", feature.Id);
        await AskSeeding.SeedQuestionAsync(ConnectionString, child.Id);
        await new WorkItemScreen(Page).OpenAsync("SHIP", feature.Key);
        var panel = new DecisionsPanel(Page, feature.Id);

        await Expect(panel.RollUpRow(child.Key)).ToContainTextAsync("1 question");
        await Expect(panel.Summary).ToContainTextAsync("Nothing here waits");
    }

    [Test]
    public async Task Decisions_WhenAnswered_ShouldShowLineInHistoryRail()
    {
        var (id, key) = await SeedOwnedItemAsync();
        await AskSeeding.SeedQuestionAsync(ConnectionString, id, "Which tenant?");
        await new WorkItemScreen(Page).OpenAsync("SHIP", key);
        var panel = new DecisionsPanel(Page, id);
        await Expect(panel.QuestionRow(1)).ToBeVisibleAsync();

        var response = await panel.ReplyAsync(1, "Tenant A");

        response.Ok.ShouldBeTrue();
        await Expect(panel.RailAskLine("Q1", "answered")).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Expect(panel.RailAskLine("Q1", "answered")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Decisions_WhenSettled_ShouldFoldToSummary()
    {
        var (id, key) = await SeedOwnedItemAsync();
        await AskSeeding.SeedDecisionsAsync(
            ConnectionString, id, new AskSeeding.DecisionSeed("Which store?", "Postgres", State: "confirmed"));
        await AskSeeding.SeedQuestionAsync(ConnectionString, id, "Which region?", "answered", "eu-west");
        await new WorkItemScreen(Page).OpenAsync("SHIP", key);
        var panel = new DecisionsPanel(Page, id);

        await Expect(panel.FoldToggle).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(panel.Summary).ToContainTextAsync("✓ Nothing waiting");
        await Expect(panel.Summary).ToContainTextAsync("1 decision: 1 confirmed · 1 question answered");
        await Expect(panel.DecisionRows).ToHaveCountAsync(0);
        await Expect(panel.BulkConfirm).ToBeHiddenAsync();

        await panel.ExpandAsync();

        await Expect(panel.DecisionRow(1)).ToBeVisibleAsync();
    }
}
