using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The payload surface as an agent sees it over /mcp: the six opted-in tools list a `payload` argument and no longer
// require their large arguments, and a call that sends neither the argument nor a payload names the argument.
[TestFixture]
public class PayloadSurfaceTests : McpFixture
{
    private static readonly string[] PayloadTools = ["attach_markdown", "break_down", "capture_plan", "capture_work", "create_feature", "refine_work_item"];

    private string _featureKey = string.Empty;
    private string _storyKey = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Payload surface").InsertAsync(connection);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Payload story").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        _storyKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
    }

    [Test]
    public async Task ListTools_WhenToolTakesPayload_ShouldAdvertisePayloadArgument()
    {
        var tools = await ListToolsAsync();

        tools.Where(tool => Properties(tool).TryGetProperty("payload", out _)).Select(Name).Order(StringComparer.Ordinal).ShouldBe(PayloadTools);
        foreach (var tool in tools.Where(tool => PayloadTools.Contains(Name(tool))))
        {
            var type = Properties(tool).GetProperty("payload").GetProperty("type");
            var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(t => t.GetString()).ToList() : [type.GetString()];
            types.ShouldContain("string", Name(tool));
            Required(tool).ShouldNotContain("payload", Name(tool));
        }
    }

    [Test]
    public async Task ListTools_WhenToolTakesPayload_ShouldNotRequireLargeArguments()
    {
        var tools = await ListToolsAsync();

        var capturePlan = Required(tools.Single(tool => Name(tool) == "capture_plan"));
        capturePlan.ShouldNotContain("shared_branch");
        capturePlan.ShouldNotContain("common_brief");
        capturePlan.ShouldNotContain("stories");
        capturePlan.ShouldNotContain("feature");
        Required(tools.Single(tool => Name(tool) == "break_down")).ShouldNotContain("children");
        Required(tools.Single(tool => Name(tool) == "attach_markdown")).ShouldNotContain("content");
    }

    [Test]
    public async Task CapturePlan_WhenCommonBriefMissing_ShouldNameTheArgument()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_plan", """
            {"project":"SHIP","feature":{"title":"No brief","goal":"Prove the check."},"shared_branch":"feat/no-brief",
             "stories":[{"title":"Only lane","wave":"1","tasks":[{"title":"Only task","brief":"Do it.","model":"strong","context_k":30}]}]}
            """);

        ShouldNameTheArgument(reply, "common_brief");
    }

    [Test]
    public async Task BreakDown_WhenChildrenMissing_ShouldNameTheArgument()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{_storyKey}}"}""");

        ShouldNameTheArgument(reply, "children");
    }

    [Test]
    public async Task AttachMarkdown_WhenContentMissing_ShouldNameTheArgument()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"empty.md"}""");

        ShouldNameTheArgument(reply, "content");
    }

    private static void ShouldNameTheArgument(McpReply reply, string field)
    {
        reply.IsToolError().ShouldBeTrue(reply.Body);
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe(field);
        error.GetProperty("message").GetString().ShouldContain("create_payload_upload");
    }

    private async Task<IReadOnlyList<JsonElement>> ListToolsAsync()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var listed = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        return listed.Json().GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
    }

    private static string Name(JsonElement tool) => tool.GetProperty("name").GetString()!;

    private static JsonElement Properties(JsonElement tool) => tool.GetProperty("inputSchema").GetProperty("properties");

    private static IReadOnlyList<string> Required(JsonElement tool)
        => tool.GetProperty("inputSchema").TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(name => name.GetString()!).ToList()
            : [];
}
