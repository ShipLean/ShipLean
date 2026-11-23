using System.Reflection;
using ModelContextProtocol.Server;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Prompts;

// SHIP-2231 C4: the three prompt tools' scopes and access rules, and the prompt-key check. Joel administers SHIP and is not in PART; Priya is a member of SHIP, not an admin.
// Nothing here writes: every call is refused, or is a preview.
[TestFixture]
public class PromptToolsContractTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private static McpScopeAttribute ScopeOf(string tool)
        => typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == tool)
            .GetCustomAttribute<McpScopeAttribute>()!;

    private static string ErrorCode(McpReply reply) => reply.Structured().GetProperty("error").GetProperty("code").GetString()!;

    private static string? ErrorField(McpReply reply) => reply.Structured().GetProperty("error").GetProperty("field").GetString();

    [Test]
    public void Prompts_WhenRegistered_ShouldNeedReadScope()
    {
        var scope = ScopeOf("prompts");

        scope.Scope.ShouldBe(McpScopes.Read);
    }

    [Test]
    public void SavePrompt_WhenRegistered_ShouldNeedPlanScopeAndPreviewWithRead()
    {
        var scope = ScopeOf("save_prompt");

        scope.Scope.ShouldBe(McpScopes.Plan);
        scope.PreviewScope.ShouldBe(McpScopes.Read);
    }

    [Test]
    public void UsePromptVersion_WhenRegistered_ShouldNeedWriteScope()
    {
        var scope = ScopeOf("use_prompt_version");

        scope.Scope.ShouldBe(McpScopes.Write);
    }

    [Test]
    public async Task Prompts_WhenNotAMember_ShouldBeNotFound()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "prompts", """{"project":"PART"}""");

        ErrorCode(reply).ShouldBe("not_found");
    }

    [Test]
    public async Task SavePrompt_WhenMemberNotAdmin_ShouldBeForbidden()
    {
        var reply = await CallToolAsync(
            await TokenAsync(TestUser.Priya, ReadWritePlan), "save_prompt",
            """{"project":"SHIP","prompt":"guide","text":"New text.","note":"A change.","based_on":"built-in"}""");

        ErrorCode(reply).ShouldBe("forbidden");
    }

    [Test]
    public async Task UsePromptVersion_WhenMemberNotAdmin_ShouldBeForbidden()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Priya, ReadWrite), "use_prompt_version", """{"project":"SHIP","prompt":"guide","version":"built-in"}""");

        ErrorCode(reply).ShouldBe("forbidden");
    }

    [Test]
    public async Task SavePrompt_WhenMemberNotAdminAndPromptUnknown_ShouldBeForbiddenFirst()
    {
        var reply = await CallToolAsync(
            await TokenAsync(TestUser.Priya, ReadWritePlan), "save_prompt",
            """{"project":"SHIP","prompt":"nope","text":"New text.","note":"A change.","based_on":"built-in"}""");

        ErrorCode(reply).ShouldBe("forbidden");
    }

    [TestCase("prompts", """{"project":"SHIP","prompt":"nope"}""")]
    [TestCase("save_prompt", """{"project":"SHIP","prompt":"nope","text":"New text.","note":"A change.","based_on":"built-in"}""")]
    [TestCase("use_prompt_version", """{"project":"SHIP","prompt":"nope","version":"built-in"}""")]
    public async Task Tool_WhenPromptUnknown_ShouldBeValidationOnPromptListingTheKeys(string tool, string arguments)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWritePlan), tool, arguments);

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("prompt");
        reply.Structured().GetProperty("error").GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("retroAnalysis");
    }

    [Test]
    public async Task UsePromptVersion_WhenVersionIsJsonNumber_ShouldBind()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWrite), "use_prompt_version", """{"project":"SHIP","prompt":"guide","version":3}""");

        reply.Structured().TryGetProperty("error", out _).ShouldBeFalse();
        reply.Structured().GetProperty("in_use_version").GetInt32().ShouldBe(3);
    }

    [Test]
    public async Task Prompts_WhenVersionIsNumericString_ShouldBind()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "prompts", """{"project":"SHIP","prompt":"guide","version":"3"}""");

        reply.Structured().TryGetProperty("error", out _).ShouldBeFalse();
        reply.Structured().GetProperty("text_of").GetString().ShouldBe("v3");
    }

    [Test]
    public async Task SavePrompt_WhenBasedOnIsBuiltIn_ShouldBind()
    {
        var reply = await CallToolAsync(
            await TokenAsync(TestUser.Joel, ReadWritePlan), "save_prompt",
            """{"project":"SHIP","prompt":"guide","text":"New text.","note":"A change.","based_on":"built-in"}""");

        reply.Structured().TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Test]
    public async Task SavePrompt_WhenBasedOnIsJsonNumber_ShouldBind()
    {
        var reply = await CallToolAsync(
            await TokenAsync(TestUser.Joel, ReadWritePlan), "save_prompt",
            """{"project":"SHIP","prompt":"guide","text":"New text.","note":"A change.","based_on":2}""");

        reply.Structured().TryGetProperty("error", out _).ShouldBeFalse();
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("2.5")]
    [TestCase("\"abc\"")]
    [TestCase("true")]
    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("[]")]
    public async Task UsePromptVersion_WhenVersionInvalid_ShouldBeValidationOnVersion(string version)
    {
        var reply = await CallToolAsync(
            await TokenAsync(TestUser.Joel, ReadWrite), "use_prompt_version", $$"""{"project":"SHIP","prompt":"guide","version":{{version}}}""");

        ErrorCode(reply).ShouldBe("validation");
        ErrorField(reply).ShouldBe("version");
    }

    [Test]
    public async Task ListTools_WhenVersionArgumentsDeclared_ShouldNotRestrictThemToString()
    {
        var listed = await PostAsync(await TokenAsync(TestUser.Joel, ReadOnly), """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        var tools = listed.Json().GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        foreach (var (tool, argument) in new[] { ("prompts", "version"), ("use_prompt_version", "version"), ("save_prompt", "based_on") })
        {
            var schema = tools.Single(t => t.GetProperty("name").GetString() == tool).GetProperty("inputSchema").GetProperty("properties").GetProperty(argument);
            schema.TryGetProperty("type", out _).ShouldBeFalse($"{tool}.{argument} advertised {schema}");
        }
    }
}
