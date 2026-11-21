using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// D11: the run page's header names the prompts the run's item was served around its time.
[TestFixture]
public class RunPromptsTests : InspectionViewsFixture
{
    [Test]
    public async Task Run_WhenPromptsServed_ShouldShowThemInTheHeader()
    {
        var versionId = await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithText("Custom brief.").InUse().InsertAsync(ConnectionString);
        await new PromptServingBuilder(SeedIds.Projects.Ship, PromptKey.Brief).WithVersion(versionId, 2).WithItemKey(Item).WithAgent(PromptAgents.ClaudeCode)
            .WithServedAt(TimeProvider.System.GetUtcNow().AddMinutes(-3)).InsertAsync(ConnectionString);
        await new PromptServingBuilder(SeedIds.Projects.Ship, PromptKey.Work).WithItemKey("SHIP-1").WithAgent(PromptAgents.ClaudeCode)
            .WithServedAt(TimeProvider.System.GetUtcNow().AddMinutes(-3)).InsertAsync(ConnectionString);

        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}");

        page.Header.Prompts.Select(p => p.Label).ShouldBe(["brief.md custom v2 · Claude Code"]);
    }

    [Test]
    public async Task Run_WhenNone_ShouldHaveAnEmptyList()
    {
        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}");

        page.Header.Prompts.ShouldBeEmpty();
    }
}
