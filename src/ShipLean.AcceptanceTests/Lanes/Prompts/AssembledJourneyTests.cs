using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.AcceptanceTests.Lanes.Prompts;

// What the agent gets: the preview per agent, and that it is exactly what the MCP endpoint serves that agent. One custom guide with a section for Claude Code
// and one for Codex CLI, seeded once for both tests (they only read).
[TestFixture]
public class AssembledJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string ClaudeLine = "Claude Code only: use the Task tool for subagents.";
    private const string GuideLine = "Estimate in tokens only, never in time.";
    private const string OpenAiLine = "OpenAI CLI only: use its sandbox flag.";

    [OneTimeSetUp]
    public async Task SeedCustomGuideAsync()
    {
        var builtIn = new BuiltInPrompts().Text(PromptKey.Guide);
        var text = builtIn.TrimEnd('\n') + $"\n\n<claude-code>\n{ClaudeLine}\n</claude-code>\n\n<codex-cli>\n{OpenAiLine}\n</codex-cli>\n";
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Guide)
            .WithNumber(1).WithText(text).WithNote("Per agent rules").InUse().InsertAsync(ConnectionString);
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Assembled_WhenPreviewedPerAgent_ShouldDiffer()
    {
        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, "guide");
        await prompts.OpenAssembledAsync();
        await Expect(prompts.AssembledText).ToContainTextAsync(GuideLine);
        await Expect(prompts.AssembledText).ToContainTextAsync(ClaudeLine);
        await Expect(prompts.AssembledText).Not.ToContainTextAsync(OpenAiLine);
        await Expect(prompts.AssembledLayerRows).ToHaveCountAsync(5);
        await Expect(prompts.AssembledLayerRows).ToContainTextAsync(new[]
        {
            new Regex("Server instructions"),
            new Regex("Planning and breakdown.*custom v1"),
            new Regex("Agent-specific sections"),
            new Regex("Ways of working"),
            new Regex("Notes for the connecting agent"),
        });

        await prompts.ChooseAgentAsync("Another agent", "other");
        await Expect(prompts.AssembledText).ToContainTextAsync(GuideLine);
        await Expect(prompts.AssembledText).Not.ToContainTextAsync(ClaudeLine);
        await Expect(prompts.AssembledText).Not.ToContainTextAsync(OpenAiLine);

        await Expect(prompts.AssembledLayerRows).ToHaveCountAsync(4);
        await Expect(prompts.AssembledPlanned).ToHaveCountAsync(2);
        await Expect(prompts.AssembledPlanned.First).ToContainTextAsync("Ways of working");
        await Expect(prompts.AssembledPlanned.Last).ToContainTextAsync("Notes for the connecting agent");
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Assembled_WhenComparedWithTheAgent_ShouldMatchWhatMcpServes()
    {
        var endpoint = new Uri($"{BaseUrl}/mcp");
        var claudeToken = await Host.IssueAgentToken(TestUser.Joel, clientName: "Test Agent");
        var claudeServed = await PromptsAdminPage.ServedGuideAsync(endpoint, claudeToken, Project, new Implementation { Name = "claude-code", Version = "1.0" });
        var otherServed = await PromptsAdminPage.ServedGuideAsync(endpoint, claudeToken, Project, new Implementation { Name = "Test Agent", Version = "1.0" });
        claudeServed.ShouldContain(ClaudeLine);
        otherServed.ShouldNotContain(ClaudeLine);

        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, "guide");
        await prompts.OpenAssembledAsync();
        await Expect(prompts.AssembledText).ToContainTextAsync(ClaudeLine);
        (await prompts.AssembledRawTextAsync()).Trim().ShouldBe(claudeServed.Trim());

        await prompts.ChooseAgentAsync("Another agent", "other");
        await Expect(prompts.AssembledText).ToContainTextAsync(GuideLine);
        await Expect(prompts.AssembledText).Not.ToContainTextAsync(ClaudeLine);
        (await prompts.AssembledRawTextAsync()).Trim().ShouldBe(otherServed.Trim());
    }
}
