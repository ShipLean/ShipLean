using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Decisions;

// A member who does not own the item answering a decision on it. Its own class: the signed-in user is per class.
[TestFixture]
public class DecisionsNonOwnerTests : SeededAcceptanceTest
{
    protected override TestUser SignedInAs => TestUser.Mei;

    [Test]
    public async Task Decisions_WhenMemberIsNotOwner_ShouldConfirm()
    {
        var (id, key) = await AskSeeding.SeedItemAsync(ConnectionString, assignee: SeedIds.Users.Joel);
        await AskSeeding.SeedDecisionsAsync(
            ConnectionString, id, new AskSeeding.DecisionSeed("Which store?", "Postgres"));
        await new WorkItemScreen(Page).OpenAsync("SHIP", key);
        var panel = new DecisionsPanel(Page, id);
        await Expect(panel.WaitingOn).ToContainTextAsync("Joel");

        var response = await panel.ConfirmAsync(1);

        Assert.That(response.Ok, Is.True);
        await Expect(panel.Toasts.Filter(new() { HasText = "D1 confirmed: “Postgres”" })).ToBeVisibleAsync();
        await Expect(panel.Summary).ToContainTextAsync("you can answer too");
        await Page.ReloadAsync();
        await Expect(panel.FoldToggle).ToBeVisibleAsync();
        await panel.ExpandAsync();
        await Expect(panel.DecisionRow(1)).ToHaveAttributeAsync("data-state", "confirmed");
    }
}
