using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Prompts;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Prompts;

// SHIP-744 AC9 (D7): the Proposal instructions box moved to the Issue proposal prompt; Integrations links to it. Joel is admin of SHIP.
[TestFixture]
public class IssueProposalJourneyTests : SeededAcceptanceTest
{
    private const string MigratedLabel = "Moved from Integrations";

    [OneTimeSetUp]
    public async Task ArrangeAMigratedProposalAsync()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.IssueProposal)
            .WithText("Write a proposal in the team's voice.")
            .WithNote("Team voice for proposals")
            .WithSource(PromptVersionSource.Migrated)
            .InUse()
            .InsertAsync(ConnectionString);
    }

    [Test]
    public async Task IssueProposal_WhenOpenedFromIntegrations_ShouldShowThePrompt()
    {
        await Page.GotoAsync(Links.ProjectSettings("SHIP", "integrations"));
        var panel = Page.GetByTestId(SettingsTestIds.IssueIntakePanel);
        var link = Page.GetByTestId(SettingsTestIds.IssueIntakePromptsLink);
        await Expect(link).ToBeVisibleAsync();
        (await panel.EvaluateAsync<int>("el => el.querySelectorAll('textarea').length")).ShouldBe(0, "the old Proposal instructions box is still in the panel");

        await link.ClickAsync();

        var prompts = new PromptsMemberPage(Page);
        await Expect(Page).ToHaveURLAsync(new Regex("/p/SHIP/settings/prompts\\?prompt=issueProposal$"));
        await Expect(prompts.DetailName).ToContainTextAsync("Issue proposal");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");
        await Page.GetByTestId(PromptsTestIds.TabHistory).ClickAsync();
        // The in-use row's note is "Team voice for proposals", so the label can only come from its Source cell (the cells carry no ids).
        await Expect(prompts.HistoryRowInUse).ToContainTextAsync("Team voice for proposals");
        await Expect(prompts.HistoryRowInUse).ToContainTextAsync(MigratedLabel);
    }
}
