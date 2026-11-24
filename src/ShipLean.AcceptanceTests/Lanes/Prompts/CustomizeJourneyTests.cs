using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Prompts;

// Joel, admin of SHIP, opens Ways of working and customizes prompts in the real editor. Each customizing test uses its own prompt, because the class shares one
// database: guide for the agent check, brief for placeholders, work for the unclosed tag, fix for the misspelt one and retro for the size limit.
[TestFixture]
public class CustomizeJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    [Test]
    [CancelAfter(240_000)]
    public async Task PromptsTab_WhenOpened_ShouldListTwelvePromptsByStage()
    {
        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project);

        await Expect(prompts.ListRows).ToHaveCountAsync(12);
        await Expect(prompts.ListStages).ToHaveCountAsync(7);
        await Expect(prompts.ListStages.First).ToContainTextAsync("Intake");
        await Expect(prompts.ListStages.Last).ToContainTextAsync("Sprint demo");

        await prompts.ListRows.Filter(new LocatorFilterOptions { HasText = "Task brief" }).ClickAsync();
        await Expect(prompts.DetailName).ToContainTextAsync("Task brief");
        await Expect(prompts.Facts).ToContainTextAsync("{{task_key}}");
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Customize_WhenSavedWithANote_ShouldReachTheAgent()
    {
        var prompts = new PromptsAdminPage(Page);
        const string FirstRule = "Project rule: run the slow suite only in CI.";
        const string SecondRule = "Project rule: never push on a Friday.";
        var token = await Host.IssueAgentToken(TestUser.Joel);
        var endpoint = new Uri($"{BaseUrl}/mcp");

        await prompts.OpenAsync(Project, "guide");
        await Expect(prompts.DetailChip).ToContainTextAsync("Built-in");
        await prompts.CustomizeAsync(FirstRule, "Slow suite stays in CI");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");
        (await PromptsAdminPage.ServedGuideAsync(endpoint, token, Project)).ShouldContain(FirstRule);

        await prompts.OpenEditorAsync();
        await prompts.AppendLineAsync(SecondRule);
        await prompts.SaveWithNoteAsync("No Friday pushes");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v2");
        var served = await PromptsAdminPage.ServedGuideAsync(endpoint, token, Project);
        served.ShouldContain(SecondRule);
        served.ShouldContain(FirstRule);
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Customize_WhenPlaceholderRemoved_ShouldWarnAndStillSave()
    {
        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, "brief");
        await prompts.OpenEditorAsync();
        var original = await prompts.EditorText.InputValueAsync();
        original.ShouldContain("{{task_key}}");

        await prompts.SetTextAsync(original.Replace("{{task_key}}", "the task", StringComparison.Ordinal));
        await Expect(prompts.PlaceholderMissing).ToBeVisibleAsync();
        await Expect(prompts.PutBack).ToBeVisibleAsync();

        await prompts.PutBack.ClickAsync();
        await Expect(prompts.PlaceholderMissing).ToHaveCountAsync(0);
        await Expect(prompts.EditorText).ToHaveValueAsync(new Regex(@"\{\{task_key\}\}"));

        await prompts.SetTextAsync(original.Replace("{{task_key}}", "the task", StringComparison.Ordinal));
        await Expect(prompts.PlaceholderMissing).ToBeVisibleAsync();
        await prompts.SaveWithNoteAsync("Dropped the key");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Customize_WhenOverTheSizeLimit_ShouldDisableSave()
    {
        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, "retro");
        await prompts.OpenEditorAsync();
        await prompts.EditorNote.FillAsync("Right at the limit");

        await prompts.SetTextAsync(new string('a', 65_537));
        await Expect(prompts.Save).ToBeDisabledAsync();
        await Expect(prompts.SizeText).ToContainTextAsync("too large to save");

        await prompts.SetTextAsync(new string('a', 65_536));
        await Expect(prompts.SizeText).Not.ToContainTextAsync("too large to save");
        await Expect(prompts.Save).ToBeEnabledAsync();
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Customize_WhenATagIsUnclosed_ShouldNotSave()
    {
        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, "work");
        await prompts.OpenEditorAsync();
        var original = await prompts.EditorText.InputValueAsync();
        var head = original.TrimEnd('\n') + "\n\n";
        var tagLine = head.Count(c => c == '\n') + 1;

        await prompts.SetTextAsync(head + "<claude-code>\nOnly for Claude Code.\n");
        await prompts.EditorNote.FillAsync("Claude Code section");
        await Expect(prompts.UnclosedTag).ToBeVisibleAsync();
        await Expect(prompts.UnclosedTag).ToContainTextAsync($"Line {tagLine}:");
        await Expect(prompts.Save).ToBeDisabledAsync();

        await prompts.SetTextAsync(head + "<claude-code>\nOnly for Claude Code.\n</claude-code>\n");
        await Expect(prompts.UnclosedTag).ToHaveCountAsync(0);
        await prompts.SaveWithNoteAsync("Claude Code section");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Customize_WhenATagIsMisspelt_ShouldOfferTheFix()
    {
        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, "fix");
        await prompts.OpenEditorAsync();
        var original = await prompts.EditorText.InputValueAsync();

        await prompts.SetTextAsync(original.TrimEnd('\n') + "\n\n<claudecode>\nOnly for Claude Code.\n");
        await Expect(prompts.Checks).ToContainTextAsync("Did you mean <claude-code>?");
        await prompts.FixTag.First.ClickAsync();

        await Expect(prompts.EditorText).ToHaveValueAsync(new Regex("<claude-code>"));
        await Expect(prompts.EditorText).Not.ToHaveValueAsync(new Regex("<claudecode>"));
        await Expect(prompts.UnknownTag).ToHaveCountAsync(0);
    }
}
