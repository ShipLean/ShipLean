using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.AcceptanceTests.Lanes.Prompts;

// History on the real page: Use built-in keeps every version and Use version puts one back, and the agent follows each move. The migrated row is its own
// prompt (issueProposal) because the class shares one database.
[TestFixture]
public class HistoryJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string BuiltInLine = "Estimate in tokens only, never in time.";

    [Test]
    [CancelAfter(240_000)]
    public async Task History_WhenBuiltInUsedThenV1Restored_ShouldServeV1()
    {
        const string V1Rule = "Version one rule: keep commits small.";
        const string V2Rule = "Version two rule: keep commits tiny.";
        var builtIn = new BuiltInPrompts().Text(PromptKey.Guide);
        var now = TimeProvider.System.GetUtcNow();
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Guide)
            .WithNumber(1).WithText(builtIn + "\n\n" + V1Rule + "\n").WithNote("First rule").WithCreatedAt(now.AddDays(-2)).InsertAsync(ConnectionString);
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Guide)
            .WithNumber(2).WithText(builtIn + "\n\n" + V2Rule + "\n").WithNote("Second rule").WithCreatedAt(now.AddDays(-1)).InUse().InsertAsync(ConnectionString);
        var token = await Host.IssueAgentToken(TestUser.Joel);
        var endpoint = new Uri($"{BaseUrl}/mcp");
        var prompts = new PromptsAdminPage(Page);

        await prompts.OpenAsync(Project, "guide");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v2");
        (await PromptsAdminPage.ServedGuideAsync(endpoint, token, Project)).ShouldContain(V2Rule);

        await prompts.UseBuiltIn.ClickAsync();
        await prompts.UseBuiltInConfirm.ClickAsync();
        await Expect(prompts.DetailChip).ToContainTextAsync("Using built-in");
        var served = await PromptsAdminPage.ServedGuideAsync(endpoint, token, Project);
        served.ShouldContain(BuiltInLine);
        served.ShouldNotContain(V1Rule);
        served.ShouldNotContain(V2Rule);

        await prompts.TabHistory.ClickAsync();
        await Expect(prompts.HistoryRows).ToHaveCountAsync(2);
        await Expect(prompts.HistoryRows.First).ToContainTextAsync("v2");
        await Expect(prompts.HistoryRows.Last).ToContainTextAsync("v1");

        await prompts.HistoryUse.Last.ClickAsync();
        await prompts.HistoryConfirm.ClickAsync();
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");
        var restored = await PromptsAdminPage.ServedGuideAsync(endpoint, token, Project);
        restored.ShouldContain(V1Rule);
        restored.ShouldNotContain(V2Rule);
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task History_WhenMigratedVersion_ShouldSayMovedFromIntegrations()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.IssueProposal)
            .WithNumber(1).WithText("Propose issues the way our team writes them.").WithNote("Our proposal rules")
            .WithSource(PromptVersionSource.Migrated).InUse().InsertAsync(ConnectionString);
        var prompts = new PromptsAdminPage(Page);

        await prompts.OpenAsync(Project, "issueProposal");
        await prompts.TabHistory.ClickAsync();

        await Expect(prompts.HistoryRows).ToHaveCountAsync(1);
        await Expect(prompts.HistoryRows.First).ToContainTextAsync("Our proposal rules");
        await Expect(prompts.HistoryRows.First).ToContainTextAsync("Moved from Integrations");
    }
}
