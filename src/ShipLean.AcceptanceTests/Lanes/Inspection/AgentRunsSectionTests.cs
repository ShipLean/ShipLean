using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Inspection;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Inspection;

// The Agent runs section on a seeded item page with three runs linked to it (one per agent), served by the fake trace source.
[TestFixture]
public class AgentRunsSectionTests : InspectionAcceptanceTest
{
    private static readonly string[] Agents = ["codex-cli-synthetic", "claude-code-synthetic", "cursor-synthetic"];

    protected override IReadOnlyList<string> FixtureNames => Agents;

    // The base starts every fixture a minute ago; this spreads them so the order is observable: Codex CLI newest, Cursor oldest.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        for (var i = 0; i < Agents.Length; i++)
        {
            Fake.Remap(Agents[i], ProjectKey, ItemKey, TimeProvider.System.GetUtcNow().AddMinutes(-(i + 1)));
        }
    };

    [Test]
    public async Task AgentRuns_WhenItemHasThreeRuns_ShouldShowThreeRowsNewestFirst()
    {
        await new WorkItemScreen(Page).OpenAsync(ProjectKey, ItemKey);

        var rows = Page.GetByTestId(InspectionTestIds.AgentRunsRow);
        await Expect(rows).ToHaveCountAsync(3);
        await Expect(Page.GetByTestId(InspectionTestIds.AgentRunsTotalRuns)).ToHaveTextAsync("3 runs");
        var agents = Page.GetByTestId(InspectionTestIds.AgentRunsRowAgent);
        await Expect(agents).ToHaveTextAsync(["Codex CLI", "Claude Code", "Cursor"]);
        await Expect(Page.GetByTestId(InspectionTestIds.AgentRunsStore)).ToContainTextAsync("Tempo and Loki");
    }

    [Test]
    public async Task AgentRuns_WhenRowAgentClicked_ShouldOpenTheRunPage()
    {
        await new WorkItemScreen(Page).OpenAsync(ProjectKey, ItemKey);

        var firstRow = Page.GetByTestId(InspectionTestIds.AgentRunsRow).First;
        var runId = await firstRow.GetAttributeAsync("data-run-id");
        await Page.GetByTestId(InspectionTestIds.AgentRunsRowAgent).First.ClickAsync();

        await Expect(Page.GetByTestId(InspectionTestIds.RunPage)).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/items/{ItemKey}/runs/{System.Text.RegularExpressions.Regex.Escape(runId!)}(\\?.*)?$"));
    }
}
