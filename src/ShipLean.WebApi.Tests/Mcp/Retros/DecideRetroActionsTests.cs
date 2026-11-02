using System.Reflection;
using ModelContextProtocol.Server;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros;

// The scope rule, the three-new-actions limit and the plan hand-off of decide_retro_actions (K8). What a preview lists and an apply writes is not tested here:
// both are placeholders until the tool is finished.
[TestFixture]
public class DecideRetroActionsTests : McpFixture
{
    private const string Feature = "SHIP-9";
    private static readonly string Points = $$"""["{{Guid.NewGuid()}}"]""";

    protected override bool WithProbeTools => false;

    [Test]
    public void Tool_ShouldBeRegisteredWithPlanScope()
    {
        var tool = typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == "decide_retro_actions");

        var scope = tool.GetCustomAttribute<McpScopeAttribute>()!;

        scope.Scope.ShouldBe(McpScopes.Plan);
        scope.PreviewScope.ShouldBe(McpScopes.Read);
    }

    [TestCase("""{"project":"SHIP"}""")]
    [TestCase("""{"project":"SHIP","sprint":"current","from":"2026-10-01","to":"2026-10-31"}""")]
    public async Task Decide_WhenNotExactlyOneScope_ShouldBeValidation(string arguments)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "decide_retro_actions", arguments);

        ErrorField(reply).ShouldBe(("validation", "scope"));
    }

    [Test]
    public async Task Decide_WhenTwoScopes_ShouldBeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "decide_retro_actions", $$"""{"project":"SHIP","feature":"{{Feature}}","sprint":"current"}""");

        ErrorField(reply).ShouldBe(("validation", "scope"));
    }

    [Test]
    public async Task Decide_WhenOnlyOneEndOfTheRange_ShouldBeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "decide_retro_actions", """{"project":"SHIP","from":"2026-10-01"}""");

        ErrorField(reply).ShouldBe(("validation", "to"));
    }

    [Test]
    public async Task Decide_WhenFourNewActions_ShouldBeRefused()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "decide_retro_actions", Arguments(Actions(4)));

        ErrorField(reply).ShouldBe(("validation", "actions"));
    }

    [Test]
    public async Task Decide_WhenThreeNewActions_ShouldPreviewWithAPlan()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "decide_retro_actions", Arguments(Actions(3)));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        result.GetProperty("applied").GetBoolean().ShouldBeFalse();
        result.GetProperty("plan").GetString().ShouldNotBeNullOrEmpty();
        result.GetProperty("expires_in_minutes").GetInt32().ShouldBe(15);
        Names(result).ShouldBe(
            ["applied", "created", "expires_in_minutes", "left_undecided", "open_url", "plan", "project", "skipped", "would_add", "would_create", "would_mark_not_needed", "would_propose_merges", "would_theme"], ignoreOrder: true);
    }

    [Test]
    public async Task Decide_WhenPlanFromAnotherUser_ShouldBeRefused()
    {
        var joel = await TokenAsync(TestUser.Joel, ReadOnly);
        var plan = (await CallToolAsync(joel, "decide_retro_actions", Arguments(Actions(1)))).Structured().GetProperty("plan").GetString()!;
        var mei = await TokenAsync(TestUser.Mei, ReadWritePlan);

        var reply = await CallToolAsync(mei, "decide_retro_actions", $$"""{"project":"SHIP","plan":"{{plan}}"}""");

        ErrorField(reply).ShouldBe(("validation", "plan"));
    }

    [Test]
    public async Task Decide_WhenOwnPlan_ShouldApply()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var plan = (await CallToolAsync(token, "decide_retro_actions", Arguments(Actions(1)))).Structured().GetProperty("plan").GetString()!;

        var reply = await CallToolAsync(token, "decide_retro_actions", $$"""{"project":"SHIP","plan":"{{plan}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("applied").GetBoolean().ShouldBeTrue();
    }

    private static string Arguments(string actions) => $$"""{"project":"SHIP","sprint":"current","actions":{{actions}}}""";

    private static string Actions(int count)
        => "[" + string.Join(",", Enumerable.Range(1, count).Select(number => $$"""{"title":"Fix {{number}}","fix_type":"skill","points":{{Points}}}""")) + "]";

    private static (string Code, string? Field) ErrorField(McpReply reply)
    {
        var error = reply.Structured().GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.TryGetProperty("field", out var field) ? field.GetString() : null);
    }

    private static string[] Names(System.Text.Json.JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];
}
