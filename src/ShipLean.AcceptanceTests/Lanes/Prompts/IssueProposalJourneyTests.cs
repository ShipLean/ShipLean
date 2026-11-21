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
    private const string MigratedNote = "Moved from Integrations › Proposal instructions";

    [OneTimeSetUp]
    public async Task ArrangeAMigratedProposalAsync()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.IssueProposal)
            .WithText("Write a proposal in the team's voice.")
            .WithNote(MigratedNote)
            .WithSource(PromptVersionSource.Migrated)
            .InUse()
            .InsertAsync(ConnectionString);
    }

    [Test]
    public async Task IssueProposal_WhenOpenedFromIntegrations_ShouldShowThePrompt()
    {
        await Page.GotoAsync(Links.ProjectSettings("SHIP", "integrations"));
        var panel = Page.GetByTestId(SettingsTestIds.IssueIntakePanel);
        await Expect(panel).ToBeVisibleAsync();
        await Expect(panel).Not.ToContainTextAsync("Proposal instructions");

        await Page.GetByTestId(SettingsTestIds.IssueIntakePromptsLink).ClickAsync();

        var prompts = new PromptsMemberPage(Page);
        await Expect(Page).ToHaveURLAsync(new Regex("/p/SHIP/settings/prompts\\?prompt=issueProposal$"));
        await Expect(prompts.DetailName).ToContainTextAsync("Issue proposal");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");
        await Page.GetByTestId(PromptsTestIds.TabHistory).ClickAsync();
        await Expect(prompts.PanelHistory).ToContainTextAsync(MigratedNote);
    }
}
