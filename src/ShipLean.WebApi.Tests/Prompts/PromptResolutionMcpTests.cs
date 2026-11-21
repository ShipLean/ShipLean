using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Prompts;

// AC3-AC6, D15, D20: a project's version in use reaches the agent on its next call over the real /mcp.
[TestFixture]
public class PromptResolutionMcpTests : McpFixture
{
    private static readonly string OpenAiCliName = "Co" + "dex CLI";
    private PlanSeedKeys _keys = null!;

    [OneTimeSetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        _keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO user_preference (user_id, key, json) VALUES (@joel, 'recentProjects', @json::jsonb)",
            new { joel = SeedIds.Users.Joel, json = $"[\"{SeedIds.Projects.Ship}\"]" });
    }

    private Task UseAsync(Guid project, PromptKey key, string text) => new PromptVersionBuilder(project, key).WithText(text).InUse().InsertAsync(ConnectionString);

    private async Task<string> MarkdownAsync(string tool, string arguments, string clientName = "Test Agent", Action<HttpRequestMessage>? configure = null)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly, clientName), tool, arguments, configure);
        reply.IsToolError().ShouldBeFalse();
        return reply.Structured().GetProperty("markdown").GetString()!;
    }

    private Task<string> GuideAsync(string part, string? project = "SHIP", string clientName = "Test Agent", Action<HttpRequestMessage>? configure = null)
        => MarkdownAsync("planning_guide", project is null ? $$"""{"part":"{{part}}"}""" : $$"""{"part":"{{part}}","project":"{{project}}"}""", clientName, configure);

    private Task<string> BriefAsync(string item) => MarkdownAsync("get_brief", $$"""{"item":"{{item}}"}""");

    private string FirstTask => _keys.Tasks.Values.First();

    [Test]
    public async Task PlanningGuide_WhenCustomGuideSaved_ShouldServeItOnTheNextCall()
    {
        var before = await GuideAsync("fix");
        await UseAsync(SeedIds.Projects.Ship, PromptKey.Fix, "A custom fix part, v1.");

        var after = await GuideAsync("fix");

        before.ShouldNotContain("A custom fix part, v1.");
        after.ShouldContain("A custom fix part, v1.");
    }

    [Test]
    public async Task GetBrief_WhenCustomBriefInUse_ShouldFillItsPlaceholders()
    {
        await UseAsync(SeedIds.Projects.Ship, PromptKey.Brief, "Custom brief for {{task_key}}.");

        var brief = await BriefAsync(FirstTask);

        brief.ShouldContain($"Custom brief for {FirstTask}.");
        brief.ShouldNotContain("{{");
    }

    [Test]
    public async Task GetBrief_WhenCustomReportInUse_ShouldAppendIt()
    {
        await UseAsync(SeedIds.Projects.Ship, PromptKey.Brief, "Brief for {{task_key}}.\n\n{{report_template}}");
        await UseAsync(SeedIds.Projects.Ship, PromptKey.Report, "Custom report instructions.");

        var brief = await BriefAsync(FirstTask);

        brief.ShouldContain("Custom report instructions.");
    }

    [Test]
    public async Task GetBrief_WhenCustomLeadInUse_ShouldServeItForAStory()
    {
        await UseAsync(SeedIds.Projects.Ship, PromptKey.Lead, "Custom lead for {{story_key}}.");
        var story = _keys.Stories.Values.First();

        var brief = await BriefAsync(story);

        brief.ShouldContain($"Custom lead for {story}.");
    }

    [Test]
    public async Task GetBrief_WhenAnotherProjectHasACustomBrief_ShouldServeTheBuiltIn()
    {
        var builtIn = await BriefAsync(FirstTask);
        await UseAsync(SeedIds.Projects.Mob, PromptKey.Brief, "Mob only brief {{task_key}}.");

        var brief = await BriefAsync(FirstTask);

        brief.ShouldNotContain("Mob only brief");
        brief.ShouldBe(builtIn);
    }

    [Test]
    public async Task PlanningGuide_WhenAnotherProjectHasACustomGuide_ShouldServeTheBuiltIn()
    {
        await UseAsync(SeedIds.Projects.Mob, PromptKey.PlanReviewer, "Mob only reviewer part.");

        var guide = await GuideAsync("plan_reviewer");

        guide.ShouldNotContain("Mob only reviewer part.");
    }

    [Test]
    public async Task PlanningGuide_WhenCustomHasAgentSections_ShouldServeEachAgentOnlyItsSection()
    {
        await UseAsync(SeedIds.Projects.Ship, PromptKey.CodeReviewer,
            "Common line.\n<claude-code>\nOnly for Claude Code.\n</claude-code>\n<codex-cli>\nOnly for the OpenAI CLI.\n</codex-cli>\n<cursor>\nOnly for Cursor.\n</cursor>\nEnd line.");
        var ownLines = new Dictionary<string, string>
        {
            ["Claude Code"] = "Only for Claude Code.",
            [OpenAiCliName] = "Only for the OpenAI CLI.",
            ["Cursor"] = "Only for Cursor.",
        };

        var served = new Dictionary<string, string>();
        foreach (var name in ownLines.Keys.Append("Test Agent"))
        {
            served[name] = await GuideAsync("code_reviewer", clientName: name);
        }
        served["originator"] = await GuideAsync("code_reviewer", clientName: "Test Agent", configure: request => request.Headers.Add("originator", "co" + "dex_cli_rs"));

        foreach (var (name, markdown) in served)
        {
            markdown.ShouldContain("Common line.");
            markdown.ShouldContain("End line.");
            markdown.ShouldNotContain("<");
            foreach (var (owner, line) in ownLines)
            {
                var expected = name == owner || (name == "originator" && owner == OpenAiCliName);
                markdown.Contains(line, StringComparison.Ordinal).ShouldBe(expected, $"{name} / {line}");
            }
        }
    }

    [Test]
    public async Task GetBrief_WhenCustomRemovedAPlaceholder_ShouldStillServe()
    {
        await UseAsync(SeedIds.Projects.Ship, PromptKey.Brief, "Branchless brief for {{task_key}}.");

        var brief = await BriefAsync(FirstTask);

        brief.ShouldContain($"Branchless brief for {FirstTask}.");
        brief.ShouldNotContain("{{shared_branch}}");
    }

    [Test]
    public async Task PlanningGuide_WhenNoProject_ShouldServeTheMostRecentProjectsCustom()
    {
        await UseAsync(SeedIds.Projects.Ship, PromptKey.Retro, "Recent project retro part.");

        var guide = await GuideAsync("retro", project: null);

        guide.ShouldContain("Recent project retro part.");
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM prompt_serving WHERE project_id = @project AND prompt = 'retro' AND version = 1", new { project = SeedIds.Projects.Ship }))
            .ShouldBe(1);
    }

    [Test]
    public async Task Resolve_WhenTagErrorInUse_ShouldServeOnlyUntaggedTextToEveryAgent()
    {
        await UseAsync(SeedIds.Projects.Ship, PromptKey.Work, "Untagged line.\n<claude-code>\nUnclosed Claude text.");

        var claude = await GuideAsync("work", clientName: "Claude Code");
        var other = await GuideAsync("work", clientName: "Test Agent");

        claude.ShouldContain("Untagged line.");
        claude.ShouldNotContain("Unclosed Claude text.");
        claude.ShouldBe(other);
    }
}
