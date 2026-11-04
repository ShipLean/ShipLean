using System.Net;
using System.Reflection;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

// create_sprint_demo's final signature and result shape over a fixed ISprintDemos; C1 makes the real one.
[TestFixture]
public class CreateSprintDemoShapeTests : McpFixture
{
    private const string Tool = "create_sprint_demo";
    private readonly FakeSprintDemos _demos = new();

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton<ISprintDemos>(_demos);
    };

    [SetUp]
    public void ResetFake() => _demos.Reset();

    [Test]
    public async Task Tool_ShouldReturnInstructionsAsMarkdownAndTheResultAsJson()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await using var connection = await OpenDatabaseAsync();
        var sprint = await connection.QuerySingleAsync<Guid>("SELECT id FROM sprint WHERE project_id = @project AND name = 'Sprint 23'", new { project = SeedIds.Projects.Ship });

        var reply = await CallToolAsync(token, Tool, """{"project":"SHIP","sprint":"Sprint 23"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.ResultText().ShouldBe("Sprint 23: the demo prompt follows. Upload before 2031-05-14 09:30 UTC; the link works once.");
        reply.Body.ShouldContain("# Make the demo");
        var result = reply.Structured();
        result.GetProperty("project").GetString().ShouldBe("SHIP");
        result.GetProperty("sprint").GetString().ShouldBe("Sprint 23");
        result.GetProperty("demo_item").GetString().ShouldBe("SHIP-9001");
        result.GetProperty("item_url").GetString().ShouldBe("https://demo.example/p/SHIP/items/SHIP-9001");
        result.GetProperty("open_url").GetString()!.ShouldEndWith($"/p/SHIP/sprints/{sprint}/demo");
        result.GetProperty("upload_url").GetString().ShouldBe("https://demo.example/api/uploads/demo?token=up123");
        result.GetProperty("upload_expires_at").GetDateTimeOffset().ShouldBe(FakeSprintDemos.Expires);
        result.GetProperty("max_video_bytes").GetInt64().ShouldBe(157286400);
        result.GetProperty("command").GetString().ShouldBe(
            "curl --fail-with-body -F chapters=@chapters.json -F script=@script.md -F video=@demo.mp4 'https://demo.example/api/uploads/demo?token=up123'");
        var brand = result.GetProperty("brand");
        brand.GetProperty("logo_is_default").GetBoolean().ShouldBeFalse();
        brand.GetProperty("logo_url").GetString().ShouldBe("https://demo.example/api/downloads/demo-logo?token=abc");
        brand.GetProperty("logo_content_type").GetString().ShouldBe("image/png");
        brand.GetProperty("logo_url_expires_at").GetDateTimeOffset().ShouldBe(FakeSprintDemos.Expires);
        brand.GetProperty("tokens_are_default").GetBoolean().ShouldBeFalse();
        brand.GetProperty("logo_fallback_reason").ValueKind.ShouldBe(JsonValueKind.Null);
        _demos.Calls.Select(c => c.SprintId).ShouldBe([sprint]);
        _demos.Calls[0].AgentStamp.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Tool_WhenSprintOmitted_ShouldUseTheMostRecentlyClosedSprint()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await using var connection = await OpenDatabaseAsync();
        var expected = await connection.QuerySingleAsync<Guid>(
            "SELECT id FROM sprint WHERE project_id = @project AND archived_at IS NULL AND rollover_status = 'decided' ORDER BY end_date DESC LIMIT 1", new { project = SeedIds.Projects.Ship });

        var reply = await CallToolAsync(token, Tool, """{"project":"SHIP"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        _demos.Calls.Select(c => c.SprintId).ShouldBe([expected]);
    }

    [Test]
    public async Task Tool_WhenSprintOmittedAndNoneClosed_ShouldUseTheCurrentSprint()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await using var connection = await OpenDatabaseAsync();
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        var current = await connection.QuerySingleAsync<Guid>(
            "SELECT id FROM sprint s WHERE s.project_id = @project AND s.archived_at IS NULL AND s.start_date <= @today AND s.end_date >= @today ORDER BY s.start_date LIMIT 1",
            new { project = SeedIds.Projects.Ship, today });
        var closed = (await connection.QueryAsync<Guid>("SELECT id FROM sprint WHERE project_id = @project AND rollover_status = 'decided'", new { project = SeedIds.Projects.Ship })).ToArray();
        await connection.ExecuteAsync("UPDATE sprint SET rollover_status = NULL WHERE id = ANY(@closed)", new { closed });
        try
        {
            var reply = await CallToolAsync(token, Tool, """{"project":"SHIP"}""");

            reply.IsToolError().ShouldBeFalse(reply.Body);
            _demos.Calls.Select(c => c.SprintId).ShouldBe([current]);
        }
        finally
        {
            await connection.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = ANY(@closed)", new { closed });
        }
    }

    [Test]
    public async Task Tool_WhenLogoFellBack_ShouldCarryTheReasonAndNoLogoLink()
    {
        _demos.Brand = FakeSprintDemos.FallbackBrand;
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, """{"project":"SHIP","sprint":"Sprint 23"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var brand = reply.Structured().GetProperty("brand");
        brand.GetProperty("logo_is_default").GetBoolean().ShouldBeTrue();
        brand.GetProperty("logo_url").ValueKind.ShouldBe(JsonValueKind.Null);
        brand.GetProperty("logo_url_expires_at").ValueKind.ShouldBe(JsonValueKind.Null);
        brand.GetProperty("tokens_are_default").GetBoolean().ShouldBeTrue();
        brand.GetProperty("logo_fallback_reason").GetString().ShouldBe("The logo is a fallback.");
    }

    [Test]
    public async Task Tool_WhenConflict_ShouldReturnTheErrorShape()
    {
        _demos.Failure = ApiException.Conflict("A demo is already being made for this sprint.");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, """{"project":"SHIP","sprint":"Sprint 23"}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Conflict);
        error.GetProperty("message").GetString().ShouldBe("A demo is already being made for this sprint.");
    }

    [Test]
    public async Task Tool_WhenClusterHost_ShouldFailLikeAttachFile()
    {
        await using var connection = await OpenDatabaseAsync();
        var original = await connection.QuerySingleAsync<string>("SELECT base_url FROM instance_settings");
        try
        {
            await SetBaseUrlAsync(connection, "http://non-prod.shiplean.svc:8080");
            var token = await TokenAsync(TestUser.Joel, ReadWrite);
            await SetBaseUrlAsync(connection, "");

            var reply = await CallToolAsync(token, Tool, """{"project":"SHIP","sprint":"Sprint 23"}""", request => request.Headers.Host = "non-prod.shiplean.svc:8080");

            reply.IsToolError().ShouldBeTrue(reply.Body);
            var error = reply.Structured().GetProperty("error");
            error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
            error.TryGetProperty("field", out var field).ShouldBeTrue();
            field.ValueKind.ShouldBe(JsonValueKind.Null);
            error.GetProperty("message").GetString().ShouldBe(
                "There is no upload URL: the only host available is a cluster service name, which a laptop cannot open. Ask an admin to set base_url to the public origin.");
            _demos.Calls.ShouldBeEmpty();
        }
        finally
        {
            await SetBaseUrlAsync(connection, original);
        }
    }

    [Test]
    public async Task Tool_ShouldBeListedWithWriteScope()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var listed = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        listed.Body.ShouldContain($"\"{Tool}\"");
        var method = typeof(ApiMarker).Assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == Tool);
        method.GetCustomAttribute<McpScopeAttribute>()!.Scope.ShouldBe(McpScopes.Write);
    }

    [Test]
    public async Task Tool_WhenReadScopeOnly_ShouldBeStepUp()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, Tool, """{"project":"SHIP","sprint":"Sprint 23"}""");

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.WwwAuthenticate.ShouldContain("insufficient_scope");
        reply.WwwAuthenticate.ShouldContain("scope=\"shiplean:read shiplean:write\"");
        _demos.Calls.ShouldBeEmpty();
    }

    private static Task SetBaseUrlAsync(System.Data.Common.DbConnection connection, string baseUrl)
        => connection.ExecuteAsync("UPDATE instance_settings SET base_url = @baseUrl", new { baseUrl });
}
