using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Auth.Proxy;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The payload filter end to end over /mcp as Joel's agents: a call with `payload` runs as if the uploaded arguments had been
// sent inline. IToolPayloads is the in-test FakeToolPayloads, each payload bound to the agent authorization that owns it.
[TestFixture]
public class PayloadFilterTests : McpFixture
{
    private const string ThreeItems = """{"items":[{"type":"story","title":"Payload item one"},{"type":"story","title":"Payload item two"},{"type":"story","title":"Payload item three"}]}""";

    private readonly ProxyTokenReaderTests.LogLines _logs = new();

    private string _featureKey = string.Empty;
    private string _storyKey = string.Empty;
    private Guid _storyId;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.RemoveAll<IToolPayloads>();
        services.AddSingleton<FakeToolPayloads>();
        services.AddSingleton<IToolPayloads>(sp => sp.GetRequiredService<FakeToolPayloads>());
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(new LoggerFactory([_logs]));
    };

    private FakeToolPayloads Payloads => Host.Services.GetRequiredService<FakeToolPayloads>();

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Payload target").InsertAsync(connection);
        _storyId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Payload story").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        _storyKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story = _storyId });
    }

    [Test]
    public async Task CapturePlan_WhenArgumentsComeFromPayload_ShouldPreviewLikeInline()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var inline = PlanArguments();
        var payload = Payloads.AddObject("capture_plan", PlanFixture.FeatureEditor.Arguments().ToJsonString(), SeedIds.Users.Joel, authorization);
        var byPayload = $$"""{"project":"SHIP","payload":"{{payload}}"}""";
        var before = await CountItemsAsync();

        var inlinePreview = await CallToolAsync(token, "capture_plan", inline.ToJsonString());
        var payloadPreview = await CallToolAsync(token, "capture_plan", byPayload);
        var applied = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{payload}}","plan":"{{Plan(payloadPreview)}}"}""");
        var afterPayloadApply = await CountItemsAsync();
        var secondPreview = await CallToolAsync(token, "capture_plan", byPayload);
        inline["plan"] = Plan(secondPreview);
        var appliedInline = await CallToolAsync(token, "capture_plan", inline.ToJsonString());

        payloadPreview.IsToolError().ShouldBeFalse(payloadPreview.Body);
        payloadPreview.Structured().GetProperty("applied").GetBoolean().ShouldBeFalse();
        payloadPreview.Structured().GetProperty("stories").GetRawText().ShouldBe(inlinePreview.Structured().GetProperty("stories").GetRawText());
        payloadPreview.Structured().GetProperty("items").GetInt32().ShouldBe(PlanFixture.FeatureEditor.ItemCount);
        applied.IsToolError().ShouldBeFalse(applied.Body);
        applied.Structured().GetProperty("applied").GetBoolean().ShouldBeTrue();
        afterPayloadApply.ShouldBe(before + PlanFixture.FeatureEditor.ItemCount);
        appliedInline.IsToolError().ShouldBeFalse(appliedInline.Body);
        (await CountItemsAsync()).ShouldBe(before + (2 * PlanFixture.FeatureEditor.ItemCount));
    }

    [Test]
    public async Task AttachMarkdown_WhenContentFieldPayload_ShouldAttachTheText()
    {
        const string text = "# Notes from a file\n\nCafé notes, line two ✓";
        var (token, authorization) = await AgentAsync(ReadWrite);
        var payload = Payloads.AddField("attach_markdown", "content", text, SeedIds.Users.Joel, authorization);

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"from-file.md","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        await using var connection = await OpenDatabaseAsync();
        var stored = await connection.QuerySingleAsync<string?>(
            "SELECT search_text FROM attachment WHERE id = @id",
            new { id = reply.Structured().GetProperty("attachment_id").GetGuid() });
        stored.ShouldBe(text);
    }

    [Test]
    public async Task BreakDown_WhenChildrenFromPayload_ShouldCreateThem()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var payload = Payloads.AddObject("break_down", """{"children":[{"title":"Child from payload one"},{"title":"Child from payload two","points":2}]}""", SeedIds.Users.Joel, authorization);

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{_storyKey}}","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        await using var connection = await OpenDatabaseAsync();
        var titles = await connection.QueryAsync<string>("SELECT title FROM work_item WHERE parent_id = @story ORDER BY title", new { story = _storyId });
        titles.ToArray().ShouldBe(new[] { "Child from payload one", "Child from payload two" });
    }

    [Test]
    public async Task RefineWorkItem_WhenInlineAndPayloadBothGiveDescription_ShouldUseInline()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var payload = Payloads.AddObject("refine_work_item", """{"title":"Title from the payload","description":"Description from the payload"}""", SeedIds.Users.Joel, authorization);

        var reply = await CallToolAsync(token, "refine_work_item", $$"""{"item":"{{_featureKey}}","description":"Description sent inline","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        await using var connection = await OpenDatabaseAsync();
        var (title, description) = await connection.QuerySingleAsync<(string, string)>("SELECT title, description FROM work_item WHERE key = @key", new { key = _featureKey });
        description.ShouldBe("Description sent inline");
        title.ShouldBe("Title from the payload");
    }

    [Test]
    public async Task CapturePlan_WhenPayloadCarriesPlan_ShouldRefuse()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var (readToken, readAuthorization) = await AgentAsync(ReadOnly);
        var plan = Plan(await CallToolAsync(token, "capture_plan", PlanArguments().ToJsonString()));
        var carrying = PlanFixture.FeatureEditor.Arguments();
        carrying["plan"] = plan;
        var payload = Payloads.AddObject("capture_plan", carrying.ToJsonString(), SeedIds.Users.Joel, authorization);
        var readPayload = Payloads.AddObject("capture_plan", carrying.ToJsonString(), SeedIds.Users.Joel, readAuthorization);
        var before = await CountItemsAsync();

        var reply = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{payload}}"}""");
        var readReply = await CallToolAsync(readToken, "capture_plan", $$"""{"project":"SHIP","payload":"{{readPayload}}"}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        ErrorOf(reply).ShouldBe((ApiErrorCodes.Validation, "payload"));
        readReply.Status.ShouldBe(HttpStatusCode.OK);
        readReply.IsToolError().ShouldBeTrue(readReply.Body);
        ErrorOf(readReply).ShouldBe((ApiErrorCodes.Validation, "payload"));
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CapturePlan_WhenPayloadCarriesPayloadKey_ShouldRefuse()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var inner = Payloads.AddObject("capture_plan", PlanFixture.FeatureEditor.Arguments().ToJsonString(), SeedIds.Users.Joel, authorization);
        var carrying = PlanFixture.FeatureEditor.Arguments();
        carrying["payload"] = inner;
        var payload = Payloads.AddObject("capture_plan", carrying.ToJsonString(), SeedIds.Users.Joel, authorization);
        var before = await CountItemsAsync();

        var reply = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        ErrorOf(reply).ShouldBe((ApiErrorCodes.Validation, "payload"));
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CaptureWork_WhenReadScopedCallerSendsPayload_ShouldNotPreview()
    {
        var (_, writeAuthorization) = await AgentAsync(ReadWrite);
        var (readToken, _) = await AgentAsync(ReadOnly);
        var payload = Payloads.AddObject("capture_work", ThreeItems, SeedIds.Users.Joel, writeAuthorization);
        var before = await CountItemsAsync();

        var reply = await CallToolAsync(readToken, "capture_work", $$"""{"project":"SHIP","payload":"{{payload}}"}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.Body.ShouldNotContain("\"plan\"");
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CapturePlan_WhenPayloadUnknown_ShouldReturnNotFound()
    {
        var (token, _) = await AgentAsync(ReadWrite);

        var reply = await CallToolAsync(token, "capture_plan", """{"project":"SHIP","payload":"pl_never_uploaded"}""");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.IsToolError().ShouldBeTrue(reply.Body);
        ErrorOf(reply).ShouldBe((ApiErrorCodes.NotFound, "payload"));
        reply.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain("create_payload_upload");
    }

    [Test]
    public async Task CapturePlan_WhenPayloadNotString_ShouldRefuse()
    {
        var (token, _) = await AgentAsync(ReadWrite);

        var reply = await CallToolAsync(token, "capture_plan", """{"project":"SHIP","payload":{"stories":[]}}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        ErrorOf(reply).ShouldBe((ApiErrorCodes.Validation, "payload"));
    }

    [Test]
    public async Task CaptureWork_WhenManyItemsFromPayload_ShouldPreview()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var payload = Payloads.AddObject("capture_work", ThreeItems, SeedIds.Users.Joel, authorization);
        var before = await CountItemsAsync();

        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("applied").GetBoolean().ShouldBeFalse();
        reply.Structured().GetProperty("items").GetArrayLength().ShouldBe(3);
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CaptureWork_WhenOneItemFromPayload_ShouldCreate()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var payload = Payloads.AddObject("capture_work", """{"items":[{"type":"story","title":"One captured from a payload"}]}""", SeedIds.Users.Joel, authorization);
        var before = await CountItemsAsync();

        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        (await CountItemsAsync()).ShouldBe(before + 1);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE title = 'One captured from a payload'")).ShouldBe(1);
    }

    [Test]
    public async Task ToolCall_WhenPayloadUsed_ShouldLookUpWithCallersUserAndAuthorization()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var payload = Payloads.AddField("attach_markdown", "content", "# Looked up", SeedIds.Users.Joel, authorization);

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"lookup.md","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        Payloads.Lookups.Where(lookup => lookup.PayloadId == payload).ToArray().ShouldBe(new[] { new FakeToolPayloads.Lookup(payload, "attach_markdown", SeedIds.Users.Joel, authorization) });
    }

    [Test]
    public async Task AttachMarkdown_WhenFieldNoLongerTextArgument_ShouldRefuse()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var payload = Payloads.AddField("attach_markdown", "children", "# Not a text argument of attach_markdown", SeedIds.Users.Joel, authorization);

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"stale-field.md","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        ErrorOf(reply).ShouldBe((ApiErrorCodes.Validation, "payload"));
    }

    [Test]
    public async Task CapturePlan_WhenNestedObjectHasPlanKey_ShouldNotRefuse()
    {
        var (token, authorization) = await AgentAsync(ReadWrite);
        var arguments = PlanFixture.FeatureEditor.Arguments();
        arguments["stories"]![0]!["plan"] = "a story's own notes, not a plan token";
        var payload = Payloads.AddObject("capture_plan", arguments.ToJsonString(), SeedIds.Users.Joel, authorization);

        var reply = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{payload}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("applied").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task ToolCall_WhenPayloadUsed_ShouldNotLogIdOrContent()
    {
        const string marker = "payload-marker-5f0c2e";
        var (token, authorization) = await AgentAsync(ReadWrite);
        var payload = Payloads.AddField("attach_markdown", "content", $"# {marker}", SeedIds.Users.Joel, authorization);
        var refused = Payloads.AddObject("capture_plan", $$"""{"plan":"{{marker}}"}""", SeedIds.Users.Joel, authorization);
        var seen = _logs.Lines.Count;

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"logged.md","payload":"{{payload}}"}""");
        var refusal = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{refused}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        refusal.IsToolError().ShouldBeTrue(refusal.Body);
        var lines = _logs.Lines.Skip(seen).ToList();
        lines.ShouldNotBeEmpty();
        lines.ShouldNotContain(line => line.Contains(payload, StringComparison.Ordinal) || line.Contains(refused, StringComparison.Ordinal) || line.Contains(marker, StringComparison.Ordinal));
    }

    [Test]
    public async Task ToolCall_WhenNoPayload_ShouldPassThroughUnchanged()
    {
        var (token, _) = await AgentAsync(ReadWrite);
        var looked = Payloads.Lookups.Count;

        var reply = await CallToolAsync(token, "capture_plan", PlanArguments().ToJsonString());

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("applied").GetBoolean().ShouldBeFalse();
        reply.Structured().GetProperty("stories").GetArrayLength().ShouldBe(PlanFixture.FeatureEditor.Stories.Count);
        Payloads.Lookups.Count.ShouldBe(looked);
    }

    private static JsonObject PlanArguments()
    {
        var arguments = PlanFixture.FeatureEditor.Arguments();
        arguments["project"] = "SHIP";
        return arguments;
    }

    private static string Plan(McpReply preview) => preview.Structured().GetProperty("plan").GetString()!;

    private static (string? Code, string? Field) ErrorOf(McpReply reply)
    {
        var error = reply.Structured().GetProperty("error");
        return (error.GetProperty("code").GetString(), error.TryGetProperty("field", out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null);
    }

    // An agent token of Joel's under a client of its own, and the authorization it carries.
    private async Task<(string Token, Guid Authorization)> AgentAsync(string[] scopes)
    {
        var client = $"Payload agent {Guid.NewGuid():N}";
        var token = await TokenAsync(TestUser.Joel, scopes, client);
        await using var connection = await OpenDatabaseAsync();
        var authorization = await connection.QuerySingleAsync<Guid>(
            "SELECT a.id FROM oauth_authorization a JOIN oauth_client c ON c.client_id = a.client_id WHERE c.client_name = @client",
            new { client });
        return (token, authorization);
    }

    private async Task<int> CountItemsAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item");
    }
}
