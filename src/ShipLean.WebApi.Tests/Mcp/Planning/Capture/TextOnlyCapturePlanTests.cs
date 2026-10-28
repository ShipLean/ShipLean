using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Capture;

// capture_plan and plan_status as a client that shows the model only text content sees them: every value the agent
// needs for its next call is read from the text blocks, never from structuredContent.
[TestFixture]
public class TextOnlyCapturePlanTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [SetUp]
    public void ActAsJoel() => Host.ActAs(SeedIds.Users.Joel);

    [Test]
    public async Task CapturePlan_WhenClientReadsOnlyText_ShouldPreviewApplyAndReadThePlanBack()
    {
        var plan = PlanFixture.FeatureEditor;
        var arguments = plan.Arguments();
        arguments["project"] = "SHIP";

        var preview = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "capture_plan", arguments.ToJsonString());
        preview.IsToolError().ShouldBeFalse(preview.Body);
        preview.Texts()[0].ShouldContain("`plan` value from the JSON below");

        var apply = (JsonObject)arguments.DeepClone();
        apply["plan"] = preview.TextJson().GetProperty("plan").GetString();
        var applied = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWrite), "capture_plan", apply.ToJsonString());

        applied.IsToolError().ShouldBeFalse(applied.Body);
        var created = applied.TextJson();
        created.GetProperty("applied").GetBoolean().ShouldBeTrue();
        var feature = created.GetProperty("feature").GetString()!;
        created.GetProperty("stories").EnumerateArray().Select(story => story.GetProperty("title").GetString())
            .ShouldBe(plan.Stories.Select(story => story.Title), ignoreOrder: true);
        created.GetProperty("stories").EnumerateArray().SelectMany(story => story.GetProperty("tasks").EnumerateArray())
            .ShouldAllBe(task => task.GetProperty("key").GetString()!.StartsWith("SHIP-"));

        var status = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "plan_status", $$"""{"item":"{{feature}}"}""");

        status.IsToolError().ShouldBeFalse(status.Body);
        var waves = status.TextJson().GetProperty("waves").EnumerateArray().ToList();
        waves.Select(wave => wave.GetProperty("wave").GetString()).ShouldBe(["0.1", "0.2", "1", "2"]);
        var tasks = waves.SelectMany(wave => wave.GetProperty("stories").EnumerateArray())
            .SelectMany(story => story.GetProperty("tasks").EnumerateArray()).ToList();
        tasks.Select(task => (task.GetProperty("title").GetString(), task.GetProperty("model").GetString(), task.GetProperty("context_k").GetInt32()))
            .ShouldBe(plan.Stories.SelectMany(story => story.Tasks).Select(task => ((string?)task.Title, (string?)task.Model, task.ContextK)), ignoreOrder: true);
    }

    [Test]
    public async Task ToolResult_ShouldCarryTheSameJsonAsTextAndAsStructuredContent()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "list_projects");

        reply.Texts().Count.ShouldBe(2);
        JsonElement.DeepEquals(reply.TextJson(), reply.Structured()).ShouldBeTrue();
    }

    [Test]
    public async Task ToolError_ShouldCarryTheMcpErrorAsJsonText()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", """{"key":"SHIP-999999"}""");

        reply.IsToolError().ShouldBeTrue();
        var texts = reply.Texts();
        texts.Count.ShouldBe(2);
        var error = reply.TextJson();
        error.GetProperty("ok").GetBoolean().ShouldBeFalse();
        error.GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
        texts[0].ShouldBe(error.GetProperty("error").GetProperty("message").GetString());
        JsonElement.DeepEquals(error, reply.Structured()).ShouldBeTrue();
    }
}
