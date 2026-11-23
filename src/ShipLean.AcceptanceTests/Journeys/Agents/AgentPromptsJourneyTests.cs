using System.Text.Json;
using ModelContextProtocol.Client;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// An agent working for a project admin reads a prompt, previews and saves a new version over MCP, and the admin then sees it in Prompts > History with
// the note, the person, the agent and "MCP". Each test opens the page after its MCP calls: the page does not refresh on an MCP write. The class shares one
// database, so each test has its own prompt (fix, lead, report); a member's agent is refused and History stays as it was.
[TestFixture]
public class AgentPromptsJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string Prompt = "fix";
    private const string UsedPrompt = "lead";
    private const string MemberPrompt = "report";
    private const string Agent = "Prompt Scribe";
    private const string Note = "Ask for a failing test first";
    private static readonly IReadOnlySet<string> Scopes = new HashSet<string> { "shiplean:read", "shiplean:write", "shiplean:plan" };

    [Test]
    [CancelAfter(240_000)]
    public async Task AgentSavesPrompt_WhenAdminOpensHistory_ShouldShowNoteAgentAndMcp()
    {
        await using var mcp = await ConnectAsync(TestUser.Joel);

        var read = await CallAsync(mcp, "prompts", new() { ["project"] = Project, ["prompt"] = Prompt });
        read.GetProperty("in_use_version").ValueKind.ShouldBe(JsonValueKind.Null);
        var text = read.GetProperty("text").GetString()! + "\n\nAlways reproduce the bug with a failing test before changing code.\n";

        var arguments = new Dictionary<string, object?>
        {
            ["project"] = Project, ["prompt"] = Prompt, ["text"] = text, ["note"] = Note, ["based_on"] = "built-in",
        };
        var preview = await CallAsync(mcp, "save_prompt", arguments);
        preview.GetProperty("applied").GetBoolean().ShouldBeFalse();
        preview.GetProperty("next_version").GetInt32().ShouldBe(1);
        arguments["plan"] = preview.GetProperty("plan").GetString();
        var applied = await CallAsync(mcp, "save_prompt", arguments);
        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        applied.GetProperty("version").GetInt32().ShouldBe(1);

        var back = await CallAsync(mcp, "prompts", new() { ["project"] = Project, ["prompt"] = Prompt });
        back.GetProperty("in_use_version").GetInt32().ShouldBe(1);
        back.GetProperty("text").GetString().ShouldBe(text);

        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, Prompt);
        await prompts.TabHistory.ClickAsync();
        await Expect(prompts.HistoryRows).ToHaveCountAsync(1);
        var row = prompts.HistoryRows.First;
        await Expect(row).ToContainTextAsync(Note);
        await Expect(row).ToContainTextAsync("Joel");
        await Expect(row).ToContainTextAsync("MCP");
        await Expect(prompts.HistoryAgent).ToContainTextAsync($"via {Agent}");
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task AgentUsesBuiltIn_WhenAdminOpensPrompt_ShouldShowBuiltInInUse()
    {
        await using var mcp = await ConnectAsync(TestUser.Joel);
        var arguments = new Dictionary<string, object?>
        {
            ["project"] = Project, ["prompt"] = UsedPrompt, ["text"] = "Reproduce the bug first, then fix it.\n", ["note"] = Note, ["based_on"] = "built-in",
        };
        var preview = await CallAsync(mcp, "save_prompt", arguments);
        arguments["plan"] = preview.GetProperty("plan").GetString();
        await CallAsync(mcp, "save_prompt", arguments);
        (await CallAsync(mcp, "prompts", new() { ["project"] = Project, ["prompt"] = UsedPrompt })).GetProperty("in_use_version").GetInt32().ShouldBe(1);

        var used = await CallAsync(mcp, "use_prompt_version", new() { ["project"] = Project, ["prompt"] = UsedPrompt, ["version"] = "built-in" });
        used.GetProperty("in_use_version").ValueKind.ShouldBe(JsonValueKind.Null);
        (await CallAsync(mcp, "prompts", new() { ["project"] = Project, ["prompt"] = UsedPrompt })).GetProperty("in_use_version").ValueKind.ShouldBe(JsonValueKind.Null);

        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, UsedPrompt);
        await Expect(prompts.DetailChip).ToContainTextAsync("Using built-in");
        await prompts.TabHistory.ClickAsync();
        await Expect(prompts.HistoryRows).ToHaveCountAsync(1);
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task MemberAgentSavesPrompt_WhenRefused_ShouldLeaveHistoryUnchanged()
    {
        await using var member = await ConnectAsync(TestUser.Priya);
        var result = await member.CallToolAsync("save_prompt", new Dictionary<string, object?>
        {
            ["project"] = Project, ["prompt"] = MemberPrompt, ["text"] = "Member wording.\n", ["note"] = Note, ["based_on"] = "built-in",
        });
        result.IsError.ShouldBe(true);
        result.StructuredContent.ShouldNotBeNull();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().ShouldBe("forbidden");

        await using var admin = await ConnectAsync(TestUser.Joel);
        var read = await CallAsync(admin, "prompts", new() { ["project"] = Project, ["prompt"] = MemberPrompt });
        read.GetProperty("in_use_version").ValueKind.ShouldBe(JsonValueKind.Null);
        read.GetProperty("versions").GetArrayLength().ShouldBe(0);

        var prompts = new PromptsAdminPage(Page);
        await prompts.OpenAsync(Project, MemberPrompt);
        await Expect(prompts.DetailChip).ToContainTextAsync("Using built-in");
        await prompts.TabHistory.ClickAsync();
        await Expect(prompts.HistoryRows).ToHaveCountAsync(0);
    }

    private async Task<McpClient> ConnectAsync(TestUser user)
    {
        var token = await Host.IssueAgentToken(user, Scopes, Agent);
        return await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}
