using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Storage;
using ShipLean.TestSupport;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.Demos;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

// create_sprint_demo over the real run: the item, the link and the live-session refusal are K6's. The prompt is a double that is only the upload link.
[TestFixture]
public class CreateSprintDemoToolTests : McpFixture
{
    private const string Tool = "create_sprint_demo";
    private readonly UploadUrlPrompt _prompt = new();

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        DemoRunCalls.UseNamedDataAndUrlPrompt(services, _prompt);
        DemoRunCalls.UseBrand(services, ProjectBrand);
    };

    // Nothing here is a default, so a hand-over that dropped the brand's source for ShipLean's own would show.
    private static readonly DemoBrand ProjectBrand = new(
        false, "<svg id=\"c1-logo\"/>", "https://logos.example/c1-project-logo.svg", "image/svg+xml", TestDates.Today.AddYears(5).AddHours(9.5),
        false, ":root { --c1-brand: #123456; }", "c1 logo fallback");

    private async Task<bool> UsableAsync(string url)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITransferTokens>().FindAsync(TransferKind.Demo, DemoRunArrange.TokenIn(url)) is not null;
    }

    private static string UploadUrlOf(McpReply reply) => reply.Structured().GetProperty("upload_url").GetString()!;

    [Test]
    public async Task CreateSprintDemo_WhenNoAgentSetUp_ShouldStillWork()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, _, name) = await DemoRunCalls.ArrangeAsync(database, Api, agent: false, closed: false);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        (await UsableAsync(UploadUrlOf(reply))).ShouldBeTrue();
    }

    [Test]
    public async Task CreateSprintDemo_ShouldReturnInstructionsInMarkdownAndUrlInJson()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var url = UploadUrlOf(reply);
        url.ShouldContain("/api/uploads/demo?token=");
        reply.Texts()[1].ShouldBe($"Upload to {url}");
        var item = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        var result = reply.Structured();
        result.GetProperty("project").GetString().ShouldBe(project.Key);
        result.GetProperty("sprint").GetString().ShouldBe(name);
        result.GetProperty("demo_item").GetString().ShouldBe(await database.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @item", new { item }));
        result.GetProperty("open_url").GetString()!.ShouldEndWith($"/p/{project.Key}/sprints/{sprintId}/demo");
        result.GetProperty("command").GetString()!.ShouldContain(url);
        result.GetProperty("max_video_bytes").GetInt64().ShouldBeGreaterThan(0);
        reply.ResultText().ShouldStartWith($"{name}: the demo prompt follows. Upload before ");
        (await database.QuerySingleAsync<(string? Via, Guid? By)>("SELECT demo_requested_via AS via, demo_requested_by AS by FROM sprint WHERE id = @sprintId", new { sprintId }))
            .ShouldBe(("mcp", SeedIds.Users.Joel));
    }

    [Test]
    public async Task CreateSprintDemo_ShouldExpireTheLinkInTwentyFourHours()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, _, name) = await DemoRunCalls.ArrangeAsync(database, Api);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));

        var left = reply.Structured().GetProperty("upload_expires_at").GetDateTimeOffset() - TimeProvider.System.GetUtcNow();
        left.ShouldBeInRange(TimeSpan.FromHours(23.5), TimeSpan.FromHours(24.1));
    }

    [Test]
    public async Task CreateSprintDemo_WhenRepeated_ShouldReuseItemAndKillTheOldLink()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var first = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));
        var item = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();

        var second = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));

        second.IsToolError().ShouldBeFalse(second.Body);
        second.Structured().GetProperty("demo_item").GetString().ShouldBe(first.Structured().GetProperty("demo_item").GetString());
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBe(item);
        (await UsableAsync(UploadUrlOf(first))).ShouldBeFalse();
        (await UsableAsync(UploadUrlOf(second))).ShouldBeTrue();
        (await DemoRunCalls.LiveTokensAsync(database, item)).ShouldBe(1);
    }

    [Test]
    public async Task CreateSprintDemo_WhenLiveSession_ShouldConflict()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api);
        using var button = await DemoRunCalls.ButtonAsync(Api, sprintId);
        button.StatusCode.ShouldBe(HttpStatusCode.OK, await button.Content.ReadAsStringAsync());
        var item = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        var sessionLink = DemoRunArrange.TokenIn(await DemoRunCalls.DeliveredBodyAsync(database, item));
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));

        reply.IsToolError().ShouldBeTrue(reply.Body);
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Conflict);
        (await UsableAsync($"token={sessionLink}")).ShouldBeTrue();
        (await DemoRunCalls.TokensAsync(database, item)).ShouldBe(1);
    }

    [Test]
    public async Task CreateSprintDemo_WhenNotMember_ShouldBeNotFound()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api);
        var token = await TokenAsync(TestUser.Priya, ReadWrite);

        var reply = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));

        reply.IsToolError().ShouldBeTrue(reply.Body);
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
    }

    [Test]
    public async Task CreateSprintDemo_WhenSprintOmitted_ShouldUseTheMostRecentlyClosed()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, older, _) = await DemoRunCalls.ArrangeAsync(database, Api);
        var newer = await DemoRunArrange.NewSprintAsync(database, project.Id, "Newest closed");
        var running = await DemoRunArrange.NewSprintAsync(database, project.Id, "Still running");
        await database.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = @newer", new { newer });
        await database.ExecuteAsync("UPDATE sprint SET end_date = (SELECT end_date FROM sprint WHERE id = @newer) + 30 WHERE id = @running", new { newer, running });
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, $$"""{"project":"{{project.Key}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("sprint").GetString().ShouldBe("Newest closed");
        (await DemoRunCalls.ItemOfAsync(database, newer)).ShouldNotBeNull();
        (await DemoRunCalls.ItemOfAsync(database, older)).ShouldBeNull();
        (await DemoRunCalls.ItemOfAsync(database, running)).ShouldBeNull();
    }

    [Test]
    public async Task CreateSprintDemo_ShouldCarryTheBrandInJson()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, _, name) = await DemoRunCalls.ArrangeAsync(database, Api);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var brand = reply.Structured().GetProperty("brand");
        brand.GetProperty("logo_is_default").GetBoolean().ShouldBeFalse();
        brand.GetProperty("logo_url").GetString().ShouldBe(ProjectBrand.LogoUrl);
        brand.GetProperty("logo_content_type").GetString().ShouldBe(ProjectBrand.LogoContentType);
        brand.GetProperty("logo_url_expires_at").GetDateTimeOffset().ShouldBe(ProjectBrand.LogoUrlExpiresAt!.Value);
        brand.GetProperty("tokens_are_default").GetBoolean().ShouldBeFalse();
        brand.GetProperty("logo_fallback_reason").GetString().ShouldBe(ProjectBrand.LogoFallbackReason);
        _prompt.LastBrand.ShouldBe(ProjectBrand);
    }

    [Test]
    public async Task CreateSprintDemo_WhenSprintArchived_ShouldBeNotFoundAndChangeNothing()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api);
        await database.ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE id = @sprintId", new { sprintId });
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, Tool, DemoRunCalls.ToolArguments(project.Key, name));

        reply.IsToolError().ShouldBeTrue(reply.Body);
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM work_item WHERE project_id = @projectId AND type = 'bau'", new { projectId = project.Id })).ShouldBe(0);
    }

    // The tool's name lookup already refuses an archived sprint, so this reaches the hand-over by id, the way a caller holding a stale id would.
    [Test]
    public async Task CreateForAgent_WhenSprintArchivedAfterItWasResolved_ShouldBeNotFoundAndChangeNothing()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, sprintId, _) = await DemoRunCalls.ArrangeAsync(database, Api);
        await database.ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE id = @sprintId", new { sprintId });
        using var scope = Host.Services.CreateScope();

        var thrown = await Should.ThrowAsync<ApiException>(
            () => scope.ServiceProvider.GetRequiredService<ISprintDemos>().CreateForAgentAsync(sprintId, "https://example.test", "c1", null));

        thrown.Code.ShouldBe(ApiErrorCodes.NotFound);
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM work_item WHERE project_id = @projectId AND type = 'bau'", new { projectId = project.Id })).ShouldBe(0);
    }
}
