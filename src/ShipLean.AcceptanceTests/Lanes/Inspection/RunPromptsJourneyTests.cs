using ShipLean.AcceptanceTests.Harness;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Prompts;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Inspection;

// SHIP-744 AC11 (D11): the run header lists the prompts the item's agents were served inside the run's window.
[TestFixture]
public class RunPromptsJourneyTests : ShipLean.AcceptanceTests.Inspection.InspectionAcceptanceTest
{
    private const string RunId = "sess-cc-1";

    // The run's start, as the fake serves it, so the servings are placed against the real window.
    private async Task<RunSummary> TheRunAsync()
    {
        var now = TimeProvider.System.GetUtcNow();
        var search = await Fake.FindRunsAsync(new RunQuery(ProjectKey, ItemKey, now.AddDays(-1), now.AddDays(1), null, null), CancellationToken.None);
        return search.Runs.Single(r => r.RunId == RunId);
    }

    private async Task OpenRunAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/p/{ProjectKey}/items/{ItemKey}/runs/{RunId}");
        await Expect(Page.GetByTestId(InspectionTestIds.RunHeader)).ToBeVisibleAsync();
    }

    [Test]
    public async Task Run_WhenAgentGotACustomBrief_ShouldShowItInInspection()
    {
        var run = await TheRunAsync();
        var v1 = await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithNumber(1).WithText("First brief.").InsertAsync(ConnectionString);
        var v2 = await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithNumber(2).WithText("Second brief.").InUse().InsertAsync(ConnectionString);
        await new PromptServingBuilder(SeedIds.Projects.Ship, PromptKey.Brief)
            .WithVersion(v2, 2).WithAgent(PromptAgents.ClaudeCode).WithItemKey(ItemKey).WithServedAt(run.StartedAt.AddSeconds(10))
            .InsertAsync(ConnectionString);
        await new PromptServingBuilder(SeedIds.Projects.Ship, PromptKey.Report)
            .WithAgent(PromptAgents.ClaudeCode).WithItemKey(ItemKey).WithServedAt(run.StartedAt.AddSeconds(20))
            .InsertAsync(ConnectionString);
        await new PromptServingBuilder(SeedIds.Projects.Ship, PromptKey.Brief)
            .WithVersion(v1, 1).WithAgent(PromptAgents.ClaudeCode).WithItemKey(ItemKey).WithServedAt(run.StartedAt.AddHours(-6))
            .InsertAsync(ConnectionString);

        await OpenRunAsync();

        var line = Page.GetByTestId(InspectionTestIds.RunPrompts);
        await Expect(line).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.RunPrompt)).ToHaveCountAsync(2);
        await Expect(line).ToContainTextAsync("brief.md custom v2 · Claude Code");
        await Expect(line).ToContainTextAsync("report.md built-in · Claude Code");
        await Expect(line).Not.ToContainTextAsync("custom v1");
    }
}
