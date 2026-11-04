using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Demos;

// K6's button, and its races against the button and the MCP tool, on the real REST and MCP paths. The sprint data and the prompt are doubles
// (the prompt is only the upload link), so the delivered body shows which link the session carries.
[TestFixture]
public class SprintDemoRunsTests : McpFixture
{
    private readonly UploadUrlPrompt _prompt = new();

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        DemoRunCalls.UseNamedDataAndUrlPrompt(services, _prompt);
    };

    private async Task<bool> UsableAsync(string token)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITransferTokens>().FindAsync(TransferKind.Demo, token) is not null;
    }

    private static async Task<ApiError> ErrorOfAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            return await ShipLean.TestSupport.Fixtures.ApiClient.ReadApiErrorAsync(response, status);
        }
    }

    [Test]
    public async Task Button_WhenClosedAndAgentReady_ShouldCreateBauAndStartSession()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api);

        using var response = await DemoRunCalls.ButtonAsync(Api, sprintId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        var item = await database.QuerySingleAsync<(string Type, Guid? SprintId, string Title)>("SELECT type, sprint_id, title FROM work_item WHERE id = @itemId", new { itemId });
        item.ShouldBe(("bau", null, $"{name} demo"));
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem().State.ShouldBe("submitted");
        var body = await DemoRunCalls.DeliveredBodyAsync(database, itemId);
        body.ShouldContain("/api/uploads/demo?token=");
        (await UsableAsync(DemoRunArrange.TokenIn(body))).ShouldBeTrue();
        (await database.QuerySingleAsync<(string? Via, Guid? By)>("SELECT demo_requested_via AS via, demo_requested_by AS by FROM sprint WHERE id = @sprintId", new { sprintId }))
            .ShouldBe(("button", SeedIds.Users.Joel));
    }

    [Test]
    public async Task Button_WhenRetried_ShouldReuseTheSameItem()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, _) = await DemoRunCalls.ArrangeAsync(database, Api);
        using var first = await DemoRunCalls.ButtonAsync(Api, sprintId);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        await DemoRunCalls.EndSessionsAsync(database, itemId);

        using var second = await DemoRunCalls.ButtonAsync(Api, sprintId);

        second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBe(itemId);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM work_item WHERE project_id = (SELECT project_id FROM sprint WHERE id = @sprintId) AND type = 'bau'", new { sprintId })).ShouldBe(1);
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Button_WhenLiveSession_ShouldConflictAndKeepTheUploadLink()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, _) = await DemoRunCalls.ArrangeAsync(database, Api);
        using var first = await DemoRunCalls.ButtonAsync(Api, sprintId);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        var token = DemoRunArrange.TokenIn(await DemoRunCalls.DeliveredBodyAsync(database, itemId));

        var error = await ErrorOfAsync(await DemoRunCalls.ButtonAsync(Api, sprintId), HttpStatusCode.Conflict);

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        (await UsableAsync(token)).ShouldBeTrue();
        (await DemoRunCalls.TokensAsync(database, itemId)).ShouldBe(1);
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Button_WhenNoConnection_ShouldBeValidationNamingIt()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api.ActingAs(TestUser.Joel), connection: false);
        var sprintId = await DemoRunArrange.NewSprintAsync(database, arranged.Project.Id);
        await database.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = @sprintId", new { sprintId });

        var error = await ErrorOfAsync(await DemoRunCalls.ButtonAsync(Api, sprintId), HttpStatusCode.BadRequest);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain("agent connection");
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
    }

    [Test]
    public async Task Button_WhenNoRepository_ShouldBeValidationNamingIt()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api.ActingAs(TestUser.Joel), repository: false);
        var sprintId = await DemoRunArrange.NewSprintAsync(database, arranged.Project.Id);
        await database.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = @sprintId", new { sprintId });

        var error = await ErrorOfAsync(await DemoRunCalls.ButtonAsync(Api, sprintId), HttpStatusCode.BadRequest);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain("repository");
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
    }

    [Test]
    public async Task Button_WhenSprintNotClosed_ShouldBeValidation()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, _) = await DemoRunCalls.ArrangeAsync(database, Api, closed: false);

        var error = await ErrorOfAsync(await DemoRunCalls.ButtonAsync(Api, sprintId), HttpStatusCode.BadRequest);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain("Close the sprint");
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
        (await database.ExecuteScalarAsync<DateTimeOffset?>("SELECT demo_requested_at FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBeNull();
    }

    [Test]
    public async Task Retry_ShouldKillTheOldUploadLink()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api);
        var agent = await TokenAsync(TestUser.Joel, ReadWrite);
        var handed = await CallToolAsync(agent, "create_sprint_demo", DemoRunCalls.ToolArguments(project.Key, name));
        handed.IsToolError().ShouldBeFalse(handed.Body);
        var oldToken = DemoRunArrange.TokenIn(handed.Structured().GetProperty("upload_url").GetString()!);
        (await UsableAsync(oldToken)).ShouldBeTrue();

        using var response = await DemoRunCalls.ButtonAsync(Api, sprintId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        (await UsableAsync(oldToken)).ShouldBeFalse();
        (await UsableAsync(DemoRunArrange.TokenIn(await DemoRunCalls.DeliveredBodyAsync(database, itemId)))).ShouldBeTrue();
        (await DemoRunCalls.LiveTokensAsync(database, itemId)).ShouldBe(1);
    }

    [Test]
    public async Task CommonSprintRead_ShouldCarryNoDemoFields()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, _) = await DemoRunCalls.ArrangeAsync(database, Api);
        using var made = await DemoRunCalls.ButtonAsync(Api, sprintId);
        made.StatusCode.ShouldBe(HttpStatusCode.OK);
        var joel = Api.ActingAs(TestUser.Joel);

        foreach (var path in new[] { $"api/sprints/{sprintId}/rollover", $"api/sprints/{sprintId}/metrics", $"api/sprints/{sprintId}/capacity" })
        {
            using var read = await joel.GetAsync(path);
            var text = await read.Content.ReadAsStringAsync();
            read.StatusCode.ShouldBe(HttpStatusCode.OK, $"{path}: {text}");
            text.ShouldNotContain("demo", Case.Insensitive, path);
        }
    }

    [Test]
    public async Task DemoWrite_ShouldPublishSprintDemoChangedNotSprintChanged()
    {
        await using var database = await OpenDatabaseAsync();
        var (project, sprintId, _) = await DemoRunCalls.ArrangeAsync(database, Api);
        var seen = new List<ProjectEvent>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(project.Id, change =>
        {
            lock (seen)
            {
                seen.Add(change);
            }

            return Task.CompletedTask;
        });

        using var made = await DemoRunCalls.ButtonAsync(Api, sprintId);
        made.StatusCode.ShouldBe(HttpStatusCode.OK);

        lock (seen)
        {
            seen.OfType<SprintDemoChanged>().ShouldHaveSingleItem().SprintId.ShouldBe(sprintId);
            seen.OfType<SprintChanged>().ShouldBeEmpty();
        }
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Race_ButtonAgainstButton_ShouldStartOneSessionWithAUsableUrlAndIssueOneToken()
    {
        await using var database = await OpenDatabaseAsync();
        for (var round = 0; round < 3; round++)
        {
            var (_, sprintId, _) = await DemoRunCalls.ArrangeAsync(database, Api);
            var barrier = new TaskCompletionSource();
            var racers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                await barrier.Task;
                return await DemoRunCalls.ButtonAsync(Api, sprintId);
            })).ToList();
            barrier.SetResult();
            var responses = await Task.WhenAll(racers);

            var won = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
            var lost = responses.Count(DemoRunCalls.IsConflict);
            responses.ToList().ForEach(r => r.Dispose());
            (won, lost).ShouldBe((1, 3));
            var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
            (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem();
            (await UsableAsync(DemoRunArrange.TokenIn(await DemoRunCalls.DeliveredBodyAsync(database, itemId)))).ShouldBeTrue();
            (await DemoRunCalls.TokensAsync(database, itemId)).ShouldBe(1);
        }
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Race_ButtonAgainstMcp_ShouldLeaveOneSessionAUsableUrlAndNoStrayToken()
    {
        await using var database = await OpenDatabaseAsync();
        var agent = await TokenAsync(TestUser.Joel, ReadWrite);
        for (var round = 0; round < 4; round++)
        {
            var (project, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api);
            var barrier = new TaskCompletionSource();
            var button = Task.Run(async () =>
            {
                await barrier.Task;
                return await DemoRunCalls.ButtonAsync(Api, sprintId);
            });
            var mcp = Task.Run(async () =>
            {
                await barrier.Task;
                return await CallToolAsync(agent, "create_sprint_demo", DemoRunCalls.ToolArguments(project.Key, name));
            });
            barrier.SetResult();
            using var pressed = await button;
            var called = await mcp;

            pressed.StatusCode.ShouldBe(HttpStatusCode.OK, await pressed.Content.ReadAsStringAsync());
            var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
            (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem();
            var sessionToken = DemoRunArrange.TokenIn(await DemoRunCalls.DeliveredBodyAsync(database, itemId));
            (await UsableAsync(sessionToken)).ShouldBeTrue();
            (await DemoRunCalls.LiveTokensAsync(database, itemId)).ShouldBe(1);
            if (called.IsToolError())
            {
                called.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Conflict);
            }
            else
            {
                (await UsableAsync(DemoRunArrange.TokenIn(called.Structured().GetProperty("upload_url").GetString()!))).ShouldBeFalse();
            }
        }
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Race_McpAgainstMcp_ShouldLeaveOneUsableUrl()
    {
        await using var database = await OpenDatabaseAsync();
        var agent = await TokenAsync(TestUser.Joel, ReadWrite);
        for (var round = 0; round < 3; round++)
        {
            var (project, sprintId, name) = await DemoRunCalls.ArrangeAsync(database, Api, agent: false, closed: false);
            var barrier = new TaskCompletionSource();
            var racers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            {
                await barrier.Task;
                return await CallToolAsync(agent, "create_sprint_demo", DemoRunCalls.ToolArguments(project.Key, name));
            })).ToList();
            barrier.SetResult();
            var replies = await Task.WhenAll(racers);

            replies.ShouldAllBe(reply => !reply.IsToolError());
            var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
            (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldBeEmpty();
            (await DemoRunCalls.LiveTokensAsync(database, itemId)).ShouldBe(1);
            var usable = 0;
            foreach (var reply in replies)
            {
                usable += await UsableAsync(DemoRunArrange.TokenIn(reply.Structured().GetProperty("upload_url").GetString()!)) ? 1 : 0;
            }

            usable.ShouldBe(1);
        }
    }
}
