using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.AcceptanceTests.Lanes.Prompts;

// A project's custom prompt that started from an older built-in: the banner, the built-in's own diff, Apply, Apply and edit and Keep mine. The class shares
// one database, so each test has its own prompt; only guide is read back over MCP.
[TestFixture]
public class BuiltInUpdateJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string ProjectRule = "Project rule: squash before merging.";

    private readonly DateTimeOffset _baseSeenAt = TimeProvider.System.GetUtcNow().AddDays(-3);

    // The server's short day: "12 Nov" in the current year, else "12 Nov 2025".
    private string BaseLabel => _baseSeenAt.ToString(
        _baseSeenAt.Year == TimeProvider.System.GetUtcNow().Year ? "d MMM" : "d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    [Test]
    [CancelAfter(240_000)]
    public async Task BuiltInUpdate_WhenOpened_ShouldShowTheBuiltInsOwnDiff()
    {
        var older = await SeedAsync(PromptKey.Brief);
        var prompts = new PromptsAdminPage(Page);

        await prompts.OpenAsync(Project, "brief");
        await Expect(prompts.DetailChip).ToContainTextAsync("Built-in updated");
        await Expect(prompts.UpdateBannerText).ToContainTextAsync("started from the " + BaseLabel + " built-in");
        await Expect(prompts.UpdateBannerText).ToContainTextAsync(new Regex(@"The built-in prompt changed on \d{1,2} \w{3}( \d{4})?\."));
        await Expect(prompts.UpdateBannerText).ToContainTextAsync("custom v1");

        await prompts.UpdateReview.ClickAsync();
        await Expect(prompts.UpdateDiff).ToContainTextAsync(older.Changed);
        await Expect(prompts.UpdateDiff).ToContainTextAsync(older.Current.Split('\n')[0]);
        await Expect(prompts.UpdateDiff).Not.ToContainTextAsync(ProjectRule);
        await Expect(prompts.AppliesCleanly).ToBeVisibleAsync();
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task BuiltInUpdate_WhenApplied_ShouldSaveANewVersion()
    {
        var older = await SeedAsync(PromptKey.Guide);
        var token = await Host.IssueAgentToken(TestUser.Joel);
        var prompts = new PromptsAdminPage(Page);

        await prompts.OpenAsync(Project, "guide");
        await prompts.UpdateReview.ClickAsync();
        var saved = await Page.RunAndWaitForResponseAsync(
            () => prompts.UpdateApply.ClickAsync(),
            r => r.Url.Contains("/prompts/guide/versions", StringComparison.Ordinal) && r.Request.Method == "POST");
        saved.Ok.ShouldBeTrue();

        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v2");
        await Expect(prompts.UpdateBanner).ToBeHiddenAsync();
        var served = await PromptsAdminPage.ServedGuideAsync(new Uri($"{BaseUrl}/mcp"), token, Project);
        served.ShouldContain(older.Current);
        served.ShouldContain(ProjectRule);
        served.ShouldNotContain(older.Changed);
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task BuiltInUpdate_WhenKeptMine_ShouldHideTheFlag()
    {
        await SeedAsync(PromptKey.Report);
        var prompts = new PromptsAdminPage(Page);

        await prompts.OpenAsync(Project, "report");
        await prompts.UpdateReview.ClickAsync();
        await prompts.UpdateKeep.ClickAsync();
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");

        await prompts.OpenAsync(Project, "report");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");
        await Expect(prompts.UpdateBanner).ToBeHiddenAsync();
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task BuiltInUpdate_WhenApplyAndEdit_ShouldOpenTheEditorWithTheMergedText()
    {
        var older = await SeedAsync(PromptKey.Lead);
        var prompts = new PromptsAdminPage(Page);

        await prompts.OpenAsync(Project, "lead");
        await prompts.UpdateReview.ClickAsync();
        await prompts.UpdateApplyEdit.ClickAsync();

        await Expect(prompts.EditorText).ToBeVisibleAsync();
        await Expect(prompts.EditorText).ToHaveValueAsync(new Regex(Regex.Escape(older.Current)));
        await Expect(prompts.EditorText).ToHaveValueAsync(new Regex(Regex.Escape(ProjectRule)));
        await Expect(prompts.EditorText).Not.ToHaveValueAsync(new Regex(Regex.Escape(older.Changed)));
    }

    // Seeds custom v1 in use: an older built-in (one middle paragraph changed) plus the project rule at the end.
    private async Task<OlderBuiltIn> SeedAsync(PromptKey key)
    {
        var current = new BuiltInPrompts().Text(key).Replace("\r\n", "\n", StringComparison.Ordinal);
        var paragraphs = current.Split("\n\n");
        var middle = paragraphs.Length / 2;
        var currentParagraph = paragraphs[middle];
        var changedParagraph = "An older paragraph the built-in has since rewritten.";
        paragraphs[middle] = changedParagraph;
        var older = string.Join("\n\n", paragraphs);
        await new PromptVersionBuilder(SeedIds.Projects.Ship, key)
            .WithNumber(1).WithText(older.TrimEnd('\n') + "\n\n" + ProjectRule + "\n").WithNote("Project rule")
            .WithBuiltInText(older).WithCreatedAt(_baseSeenAt).InUse().InsertAsync(ConnectionString);
        return new OlderBuiltIn(currentParagraph, changedParagraph);
    }

    private sealed record OlderBuiltIn(string Current, string Changed);
}
