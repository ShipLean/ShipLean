using System.Net;
using System.Reflection;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Payloads;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros.Analysis;

// record_retro_analysis (C2): the tool surface, the plan token (a digest of the arguments, as capture_plan seals it) and the shape of the result, over the fake writer.
[TestFixture]
public class RecordRetroAnalysisContractTests : McpFixture
{
    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.RemoveAll<IToolPayloads>();
        services.AddSingleton<FakeToolPayloads>();
        services.AddSingleton<IToolPayloads>(sp => sp.GetRequiredService<FakeToolPayloads>());
    };

    private FakeToolPayloads Payloads => Host.Services.GetRequiredService<FakeToolPayloads>();

    [OneTimeSetUp]
    public async Task ArrangeFeature()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Record contract").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
    }

    [Test]
    public void Tool_WhenListed_ShouldHaveItsScope()
    {
        var tool = typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == "record_retro_analysis");

        var scope = tool.GetCustomAttribute<McpScopeAttribute>()!;

        scope.Scope.ShouldBe(McpScopes.Plan);
        scope.PreviewScope.ShouldBe(McpScopes.Read);
    }

    [Test]
    public async Task Preview_ShouldReturnAPlanAndTheC2Shape()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "record_retro_analysis", Arguments("Fix one"));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        result.GetProperty("applied").GetBoolean().ShouldBeFalse();
        result.GetProperty("plan").GetString().ShouldNotBeNullOrEmpty();
        result.GetProperty("expires_in_minutes").GetInt32().ShouldBe(15);
        Names(result).ShouldBe(
            ["project", "open_url", "applied", "funnel", "patterns", "would_create", "would_add", "skipped", "warnings", "analysis_id", "created", "plan", "expires_in_minutes"], ignoreOrder: true);
        Names(result.GetProperty("funnel")).ShouldBe(
            ["points_in", "noise", "folded", "one_off", "generic", "clustered", "clustered_percent", "unclustered", "patterns_problem", "patterns_went_well", "fixes_proposed", "already_enforced_fully", "already_enforced_partly", "discarded", "actions_new", "actions_added"],
            ignoreOrder: true);
        var pattern = result.GetProperty("patterns")[0];
        Names(pattern).ShouldBe(["ref", "kind", "title", "rank", "continues", "evidence"], ignoreOrder: true);
        pattern.GetProperty("ref").GetString().ShouldBe("p1");
        Names(pattern.GetProperty("evidence")).ShouldBe(["points", "items", "features", "sprints", "agents", "first_logged", "last_logged"], ignoreOrder: true);
        Names(result.GetProperty("would_create")[0]).ShouldBe(["title", "fix_type", "theme", "covers", "possible_duplicates"], ignoreOrder: true);
    }

    [Test]
    public async Task Apply_WhenSameArguments_ShouldApply()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var preview = await CallToolAsync(token, "record_retro_analysis", Arguments("Fix one"));

        var applied = await CallToolAsync(token, "record_retro_analysis", Arguments("Fix one", Plan(preview)));

        applied.IsToolError().ShouldBeFalse(applied.Body);
        applied.Structured().GetProperty("applied").GetBoolean().ShouldBeTrue();
        applied.Structured().GetProperty("analysis_id").GetGuid().ShouldNotBe(Guid.Empty);
        applied.Structured().GetProperty("created").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task Apply_WhenArgumentsChanged_ShouldBeRefused()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var preview = await CallToolAsync(token, "record_retro_analysis", Arguments("Fix one"));

        var refused = await CallToolAsync(token, "record_retro_analysis", Arguments("Fix two", Plan(preview)));

        ErrorField(refused).ShouldBe(("validation", "plan"));
    }

    [Test]
    public async Task Apply_WhenReadOnlyToken_ShouldBeForbidden()
    {
        var preview = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "record_retro_analysis", Arguments("Fix one"));

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "record_retro_analysis", Arguments("Fix one", Plan(preview)));

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Preview_WhenSentAsPayload_ShouldMatchInline()
    {
        var (token, authorization) = await AgentAsync(ReadWritePlan);
        var inline = Arguments("Fix one");
        var payload = Payloads.AddObject("record_retro_analysis", inline, SeedIds.Users.Joel, authorization);
        var byPayload = $$"""{"project":"SHIP","payload":"{{payload}}"}""";

        var inlinePreview = await CallToolAsync(token, "record_retro_analysis", inline);
        var payloadPreview = await CallToolAsync(token, "record_retro_analysis", byPayload);
        var applied = await CallToolAsync(token, "record_retro_analysis", $$"""{"project":"SHIP","payload":"{{payload}}","plan":"{{Plan(inlinePreview)}}"}""");

        payloadPreview.IsToolError().ShouldBeFalse(payloadPreview.Body);
        payloadPreview.Structured().GetProperty("funnel").GetRawText().ShouldBe(inlinePreview.Structured().GetProperty("funnel").GetRawText());
        payloadPreview.Structured().GetProperty("patterns").GetRawText().ShouldBe(inlinePreview.Structured().GetProperty("patterns").GetRawText());
        applied.IsToolError().ShouldBeFalse(applied.Body);
        applied.Structured().GetProperty("applied").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task Preview_WhenAClusteredPointIsInNoPattern_ShouldNamePoint()
    {
        var point = Guid.NewGuid();
        var arguments = $$"""
            {"project":"SHIP","feature":"{{_featureKey}}","embedding_model":"e5-small","report_markdown":"# Report",
             "points":[{"id":"{{point}}","outcome":"clustered","generic":"Run the check first."}]}
            """;

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "record_retro_analysis", arguments);

        ErrorField(reply).ShouldBe(("validation", "points[0].outcome"));
    }

    [Test]
    public async Task Preview_WhenFixTypeIsUnknown_ShouldNameFixType()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "record_retro_analysis", Arguments("Fix one", fixType: "wish"));

        ErrorField(reply).ShouldBe(("validation", "fixes[0].fix_type"));
    }

    [Test]
    public async Task Preview_WhenNoScope_ShouldBeValidationNamingScope()
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "record_retro_analysis", """{"project":"SHIP","embedding_model":"e5-small","report_markdown":"# Report","points":[]}""");

        ErrorField(reply).ShouldBe(("validation", "scope"));
    }

    private string Arguments(string fixTitle, string? plan = null, string fixType = "analyzer")
    {
        var point = Guid.Parse("a0000000-0000-4000-8000-0000000000aa");
        var planArgument = plan is null ? string.Empty : $",\"plan\":\"{plan}\"";
        return $$"""
            {"project":"SHIP","feature":"{{_featureKey}}","embedding_model":"e5-small","rewrite_model":"claude-sonnet","report_markdown":"# Report",
             "points":[{"id":"{{point}}","outcome":"clustered","generic":"Run the check before the step it guards."}],
             "patterns":[{"ref":"p1","kind":"problem","title":"Checks run too late","lesson":"Run the check first.","asks":["Move the check"],"point_ids":["{{point}}"]}],
             "fixes":[{"pattern_ref":"p1","title":"{{fixTitle}}","fix_type":"{{fixType}}","enforcement":"notCaught","reason":"Nothing checks it."}]{{planArgument}}}
            """;
    }

    private static string Plan(McpReply preview) => preview.Structured().GetProperty("plan").GetString()!;

    private async Task<(string Token, Guid Authorization)> AgentAsync(string[] scopes)
    {
        var client = $"Record agent {Guid.NewGuid():N}";
        var token = await TokenAsync(TestUser.Joel, scopes, client);
        await using var connection = await OpenDatabaseAsync();
        var authorization = await connection.QuerySingleAsync<Guid>(
            "SELECT a.id FROM oauth_authorization a JOIN oauth_client c ON c.client_id = a.client_id WHERE c.client_name = @client",
            new { client });
        return (token, authorization);
    }

    private static (string Code, string? Field) ErrorField(McpReply reply)
    {
        var error = reply.Structured().GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.TryGetProperty("field", out var field) ? field.GetString() : null);
    }

    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];
}
