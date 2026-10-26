using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Scopes;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning;

// The planning tools and prompt as an agent sees them: listed with their scopes, resolving their project or item under
// the access rule, and the plan shape rules registered. Joel is a member of SHIP; SHIP-99999 does not exist.
[TestFixture]
public class PlanningSurfaceTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private const string Missing = "SHIP-99999";

    // A plan every shape rule accepts: one lane-less wave with one story and one task.
    private const string Plan = """
        {"project":"SHIP","feature":{"title":"Surface plan","goal":"Prove the scopes.","description":"## Contracts\n\nNone."},
         "shared_branch":"feat/surface-plan","common_brief":"Read AGENTS.md first.",
         "stories":[{"title":"Only lane","wave":"1","tasks":[{"title":"Only task","brief":"Do the one thing and test it.","model":"strong","context_k":30}]}]}
        """;

    // Each tool's scope without a plan argument and with one.
    private static readonly Dictionary<string, (string Preview, string Apply)> Scopes = new()
    {
        ["planning_guide"] = (McpScopes.Read, McpScopes.Read),
        ["capture_plan"] = (McpScopes.Read, McpScopes.Write),
        ["get_brief"] = (McpScopes.Read, McpScopes.Read),
        ["report_task"] = (McpScopes.Write, McpScopes.Write),
        ["plan_status"] = (McpScopes.Read, McpScopes.Read),
    };

    [Test]
    public async Task PlanningTools_ShouldBeListedWithTheirScopes()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var listed = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        var names = listed.Json().GetProperty("result").GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        foreach (var (name, (preview, apply)) in Scopes)
        {
            names.ShouldContain(name);
            var tool = Tool(name);
            ToolScopes.Required(tool, new Dictionary<string, JsonElement>()).ShouldBe(preview, name);
            ToolScopes.Required(tool, new Dictionary<string, JsonElement> { ["plan"] = JsonSerializer.SerializeToElement("sealed") }).ShouldBe(apply, name);
        }
    }

    [Test]
    public async Task PlanFeaturePrompt_ShouldBeListed()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var listed = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"prompts/list"}""");

        var prompt = listed.Json().GetProperty("result").GetProperty("prompts").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "plan_feature");
        prompt.GetProperty("arguments").EnumerateArray().Select(a => a.GetProperty("name").GetString()).ShouldBe(["feature", "description"]);
    }

    [Test]
    public void PlanShapeRules_ShouldBeRegistered()
    {
        using var scope = Host.Services.CreateScope();

        scope.ServiceProvider.GetService<IPlanShapeRules>().ShouldNotBeNull();
    }

    // The guide resolves a project, not an item.
    [Test]
    public async Task PlanningGuide_UnknownItem_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide", """{"project":"NOPE"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task PlanningGuide_WithoutProject_ShouldNeedNoProject()
    {
        var token = await TokenAsync(TestUser.Kai, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide", "{}");

        ErrorCode(reply).ShouldBe("ok");
        reply.Structured().GetProperty("lessons").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task CapturePlan_UnknownItem_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "capture_plan", Plan.Replace("\"feature\":{", $"\"feature\":{{\"key\":\"{Missing}\",", StringComparison.Ordinal));

        ErrorCode(reply).ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task CapturePlan_WithoutPlan_ShouldNeedOnlyRead()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "capture_plan", Plan);

        reply.Status.ShouldBe(HttpStatusCode.OK);
        ErrorCode(reply).ShouldBe("ok");
        reply.Structured().GetProperty("applied").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task CapturePlan_WithPlan_ShouldNeedWriteAndCountTowardsTheWriteLimit()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var sealedPlan = (await CallToolAsync(token, "capture_plan", Plan)).Structured().GetProperty("plan").GetString()!;
        var apply = Plan.Replace("{\"project\":\"SHIP\",", $"{{\"project\":\"SHIP\",\"plan\":\"{sealedPlan}\",", StringComparison.Ordinal);

        var reply = await CallToolAsync(token, "capture_plan", apply);

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.WwwAuthenticate.ShouldContain("insufficient_scope");
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(apply)!;
        ToolScopes.IsWriting(ToolScopes.Required(Tool("capture_plan"), arguments)).ShouldBeTrue();
    }

    [Test]
    public async Task GetBrief_UnknownItem_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "get_brief", $$"""{"item":"{{Missing}}"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task ReportTask_UnknownItem_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "report_task", $$"""{"item":"{{Missing}}","status":"done"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task PlanStatus_UnknownItem_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "plan_status", $$"""{"item":"{{Missing}}"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.NotFound);
    }

    private McpServerTool Tool(string name)
        => Host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!.Single(tool => tool.ProtocolTool.Name == name);

    private static string? ErrorCode(McpReply reply)
        => reply.IsToolError() ? reply.Structured().GetProperty("error").GetProperty("code").GetString() : "ok";
}
