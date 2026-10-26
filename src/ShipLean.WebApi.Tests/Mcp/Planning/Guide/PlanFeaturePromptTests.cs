using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Guide;

// plan_feature and the prompts' access: any valid token may list and get one, and a failure is a JSON-RPC error that keeps its code.
[TestFixture]
public class PlanFeaturePromptTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private string _feature = null!;
    private string _story = null!;

    [OneTimeSetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        _feature = keys.Feature;
        _story = keys.Stories.Values.First();
    }

    [Test]
    public async Task PlanFeaturePrompt_WithFeatureKey_ShouldNameItAndPlanningGuide()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await GetAsync(token, $$"""{"feature":"{{_feature.ToLowerInvariant()}}"}""");

        var text = MessageText(reply);
        text.ShouldContain(_feature);
        text.ShouldContain("planning_guide");
    }

    [Test]
    public async Task PlanFeaturePrompt_WithDescription_ShouldCarryIt()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await GetAsync(token, """{"description":"Let people export a board as a PDF."}""");

        var text = MessageText(reply);
        text.ShouldContain("Let people export a board as a PDF.");
        text.ShouldContain("planning_guide");
    }

    [Test]
    public async Task PlanFeaturePrompt_UnknownFeature_ShouldBeAnError()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await GetAsync(token, """{"feature":"SHIP-99999"}""");

        reply.Json().GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32602);
        var data = reply.Json().GetProperty("error").GetProperty("data");
        data.GetProperty("ok").GetBoolean().ShouldBeFalse();
        data.GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
        data.GetProperty("error").GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task PlanFeaturePrompt_WithStoryKey_ShouldBeValidationOnFeature()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await GetAsync(token, $$"""{"feature":"{{_story}}"}""");

        reply.Json().GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32602);
        var error = reply.Json().GetProperty("error").GetProperty("data").GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("validation");
        error.GetProperty("field").GetString().ShouldBe("feature");
    }

    [Test]
    public async Task PlanFeaturePrompt_NeitherArgument_ShouldBeAnError()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await GetAsync(token, "{}");

        reply.Json().GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32602);
        reply.Json().GetProperty("error").GetProperty("data").GetProperty("error").GetProperty("code").GetString().ShouldBe("validation");
        reply.Json().GetProperty("error").GetProperty("data").GetProperty("error").GetProperty("field").GetString().ShouldBe("feature");
    }

    [Test]
    public async Task Prompts_WithReadOnlyToken_ShouldListAndGet()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var listed = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"prompts/list"}""");
        var got = await GetAsync(token, """{"description":"Anything."}""");

        listed.Json().GetProperty("result").GetProperty("prompts").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ShouldContain("plan_feature");
        MessageText(got).ShouldContain("Anything.");
    }

    [Test]
    public async Task Prompts_WithoutToken_ShouldBe401()
    {
        var listed = await PostAsync(null, """{"jsonrpc":"2.0","id":1,"method":"prompts/list"}""");
        var got = await PostAsync(null, """{"jsonrpc":"2.0","id":1,"method":"prompts/get","params":{"name":"plan_feature","arguments":{}}}""");

        listed.Status.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
        got.Status.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    private Task<McpReply> GetAsync(string token, string arguments)
        => PostAsync(token, $$$"""{"jsonrpc":"2.0","id":1,"method":"prompts/get","params":{"name":"plan_feature","arguments":{{{arguments}}}}}""");

    private static string MessageText(McpReply reply)
        => reply.Json().GetProperty("result").GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;
}
