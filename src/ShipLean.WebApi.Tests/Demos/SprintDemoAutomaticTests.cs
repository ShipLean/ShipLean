using System.Net;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Events;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Application.Storage;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Demos;

// K6's automatic path on the real one: SprintClosed on the bus, the subscriber's scope with no request, the claim, the decision. The prompt is only
// the upload link, so the delivered body shows which link the session carries. A test waits on the recorded run, never on the clock.
[TestFixture]
public class SprintDemoAutomaticTests : McpFixture
{
    private static int _next;
    private readonly UploadUrlPrompt _prompt = new();
    private readonly RecordedAutomaticRuns _runs = new();
    private readonly RecordedLogs _logs = new();
    private RecordedDemoWrites? _writes;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        DemoRunCalls.UseNamedDataAndUrlPrompt(services, _prompt);
        services.AddSingleton(_runs);
        services.RemoveAll<IProjectEvents>();
        services.AddSingleton<IProjectEvents>(provider =>
        {
            var writes = new RecordedDemoWrites(ActivatorUtilities.CreateInstance<InMemoryProjectEvents>(provider), provider.GetRequiredService<NpgsqlDataSource>());
            _writes = writes;
            return writes;
        });
        services.RemoveAll<ISprintDemos>();
        services.AddScoped<ISprintDemos>(provider => new RecordingSprintDemos(
            ActivatorUtilities.CreateInstance<ShipLean.Application.Features.Demos.SprintDemos>(provider), _runs));
    };

    [TearDown]
    public void ResetDoubles()
    {
        _prompt.Fail = false;
        _runs.Fail = false;
    }

    // A closed-by-the-bus project: agent and repository ready, a sprint whose dates are past, and the automatic flag as given.
    private async Task<(Guid ProjectId, Guid SprintId, string Name, string Key)> ArrangeAsync(NpgsqlConnection database, bool? automatic, bool agent = true, bool repository = true)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api.ActingAs(TestUser.Joel), connection: agent, repository: repository);
        var name = $"Spr {Guid.NewGuid():N}"[..14];
        var sprintId = Guid.NewGuid();
        var start = new DateOnly(2000, 1, 1).AddDays(14 * Interlocked.Increment(ref _next));
        await database.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date, rollover_status) VALUES (@sprintId, @projectId, @name, @start, @end, 'pending')",
            new { sprintId, projectId = arranged.Project.Id, name, start, end = start.AddDays(13) });
        if (automatic is { } on)
        {
            await database.ExecuteAsync(
                "INSERT INTO project_demo_settings (project_id, automatic) VALUES (@projectId, @on) ON CONFLICT (project_id) DO UPDATE SET automatic = @on",
                new { projectId = arranged.Project.Id, on });
        }

        return (arranged.Project.Id, sprintId, name, arranged.Project.Key);
    }

    private Task PublishClosedAsync(Guid projectId, Guid sprintId, Guid closedBy)
        => Host.Services.GetRequiredService<IProjectEvents>().PublishAsync(new SprintClosed(projectId, sprintId, closedBy));

    private async Task CloseByEventAsync(Guid projectId, Guid sprintId, Guid closedBy, int runs = 1)
    {
        await PublishClosedAsync(projectId, sprintId, closedBy);
        await _runs.WaitAsync(sprintId, runs);
    }

    private IReadOnlyList<(bool Requested, string? Message)> WritesOf(Guid sprintId) => _writes.ShouldNotBeNull().Of(sprintId);

    private async Task<bool> UsableAsync(string token)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITransferTokens>().FindAsync(TransferKind.Demo, token) is not null;
    }

    private static async Task<(Guid? By, string? Via, DateTimeOffset? At, string? Message)> RequestOfAsync(NpgsqlConnection database, Guid sprintId)
        => await database.QuerySingleAsync<(Guid? By, string? Via, DateTimeOffset? At, string? Message)>(
            "SELECT demo_requested_by AS by, demo_requested_via AS via, demo_requested_at AS at, demo_blocked_message AS message FROM sprint WHERE id = @sprintId", new { sprintId });

    private static async Task<int> BauItemsAsync(NpgsqlConnection database, Guid projectId)
        => await database.ExecuteScalarAsync<int>("SELECT count(*) FROM work_item WHERE project_id = @projectId AND type = 'bau'", new { projectId });

    private static async Task<int> SessionsInProjectAsync(NpgsqlConnection database, Guid projectId)
        => await database.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM agent_session s JOIN work_item w ON w.id = s.work_item_id WHERE w.project_id = @projectId", new { projectId });

    private static async Task<string?> BaseUrlAsync(NpgsqlConnection database) => await database.ExecuteScalarAsync<string?>("SELECT base_url FROM instance_settings");

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_WhenSettingOff_ShouldDoNothing()
    {
        await using var database = await OpenDatabaseAsync();
        var explicitOff = await ArrangeAsync(database, automatic: false);
        var noRow = await ArrangeAsync(database, automatic: null);

        await CloseByEventAsync(explicitOff.ProjectId, explicitOff.SprintId, SeedIds.Users.Joel);
        await CloseByEventAsync(noRow.ProjectId, noRow.SprintId, SeedIds.Users.Joel);

        foreach (var arranged in new[] { explicitOff, noRow })
        {
            (await RequestOfAsync(database, arranged.SprintId)).ShouldBe((null, null, null, null));
            (await DemoRunCalls.ItemOfAsync(database, arranged.SprintId)).ShouldBeNull();
            (await BauItemsAsync(database, arranged.ProjectId)).ShouldBe(0);
            (await SessionsInProjectAsync(database, arranged.ProjectId)).ShouldBe(0);
        }
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_WhenOn_ShouldStartSessionAsCloser()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, name, _) = await ArrangeAsync(database, automatic: true);

        await CloseByEventAsync(projectId, sprintId, SeedIds.Users.Kai);

        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        (await database.QuerySingleAsync<(string Type, string Title)>("SELECT type, title FROM work_item WHERE id = @itemId", new { itemId })).ShouldBe(("bau", $"{name} demo"));
        var session = (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem();
        (session.State, session.CreatedBy).ShouldBe(("submitted", SeedIds.Users.Kai));
        var request = await RequestOfAsync(database, sprintId);
        (request.By, request.Via, request.Message).ShouldBe((SeedIds.Users.Kai, "close", null));
        request.At.ShouldNotBeNull();
        var body = await DemoRunCalls.DeliveredBodyAsync(database, itemId);
        body.ShouldContain("/api/uploads/demo?token=");
        (await UsableAsync(DemoRunArrange.TokenIn(body))).ShouldBeTrue();
        (await DemoRunCalls.LiveTokensAsync(database, itemId)).ShouldBe(1);
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_WhenAlreadyRequested_ShouldDoNothing()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true);
        await database.ExecuteAsync(
            "UPDATE sprint SET demo_requested_by = @by, demo_requested_via = 'mcp', demo_requested_at = now() - interval '1 hour' WHERE id = @sprintId", new { by = SeedIds.Users.Priya, sprintId });
        var before = await RequestOfAsync(database, sprintId);

        await CloseByEventAsync(projectId, sprintId, SeedIds.Users.Kai);

        (await RequestOfAsync(database, sprintId)).ShouldBe(before);
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
        (await SessionsInProjectAsync(database, projectId)).ShouldBe(0);
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_WhenBlocked_ShouldStoreMessageAndNotThrow()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true, agent: false);
        using var scope = Host.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ISprintDemos>().StartAutomaticAsync(projectId, sprintId, SeedIds.Users.Kai);

        var request = await RequestOfAsync(database, sprintId);
        request.Message.ShouldNotBeNull().ShouldContain("agent connection");
        (request.By, request.Via).ShouldBe((SeedIds.Users.Kai, "close"));
        // K14: one publish after the claim (no message yet), one after the blocked message is stored.
        WritesOf(sprintId).ShouldBe([(true, null), (true, request.Message)]);
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_WhenBlocked_ShouldMakeNoItem()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true, repository: false);

        await CloseByEventAsync(projectId, sprintId, SeedIds.Users.Kai);

        (await RequestOfAsync(database, sprintId)).Message.ShouldNotBeNull().ShouldContain("repository");
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
        (await BauItemsAsync(database, projectId)).ShouldBe(0);
        (await SessionsInProjectAsync(database, projectId)).ShouldBe(0);
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM transfer_token t JOIN work_item w ON w.id = t.work_item_id WHERE w.project_id = @projectId", new { projectId })).ShouldBe(0);
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_WhenBaseUrlBlank_ShouldStoreMessage()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true);
        var stored = await BaseUrlAsync(database);
        await database.ExecuteAsync("UPDATE instance_settings SET base_url = ''");
        try
        {
            await CloseByEventAsync(projectId, sprintId, SeedIds.Users.Kai);
        }
        finally
        {
            await database.ExecuteAsync("UPDATE instance_settings SET base_url = @stored", new { stored });
        }

        var blank = await RequestOfAsync(database, sprintId);
        blank.Message.ShouldNotBeNull().ShouldContain("base URL");
        WritesOf(sprintId).ShouldBe([(true, null), (true, blank.Message)]);
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
        (await BauItemsAsync(database, projectId)).ShouldBe(0);
        (await SessionsInProjectAsync(database, projectId)).ShouldBe(0);
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_WhenEventDeliveredTwice_ShouldRunOnce()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true);

        await CloseByEventAsync(projectId, sprintId, SeedIds.Users.Kai);
        var first = await RequestOfAsync(database, sprintId);
        first.At.ShouldNotBeNull();

        // The second delivery names another closer. A claim that works leaves the first request exactly as it was (the starter's session
        // uniqueness would hide a claim that won twice, so the claim itself is what this asserts).
        await CloseByEventAsync(projectId, sprintId, SeedIds.Users.Priya, runs: 2);

        (await RequestOfAsync(database, sprintId)).ShouldBe(first);
        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem();
        (await DemoRunCalls.TokensAsync(database, itemId)).ShouldBe(1);
        (await BauItemsAsync(database, projectId)).ShouldBe(1);
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_ShouldRunWithNoRequestUser()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true);
        using var scope = Host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ICurrentUser>().Id);

        await scope.ServiceProvider.GetRequiredService<ISprintDemos>().StartAutomaticAsync(projectId, sprintId, SeedIds.Users.Tom);

        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem().CreatedBy.ShouldBe(SeedIds.Users.Tom);
        (await RequestOfAsync(database, sprintId)).Message.ShouldBeNull();
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Automatic_WhenStartThrows_ShouldStoreAFixedMessageAndLeaveNoTokenOrSession()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true);
        _prompt.Fail = true;

        await CloseByEventAsync(projectId, sprintId, SeedIds.Users.Kai);

        (await RequestOfAsync(database, sprintId)).Message.ShouldBe(SprintDemoAutomaticRun.FailedMessage);
        WritesOf(sprintId).ShouldContain((true, SprintDemoAutomaticRun.FailedMessage));
        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldBeEmpty();
        (await DemoRunCalls.TokensAsync(database, itemId)).ShouldBe(0);
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Race_AutomaticAgainstButton_ShouldLeaveOneSessionAUsableUrlAndNoStrayToken()
    {
        await using var database = await OpenDatabaseAsync();
        for (var round = 0; round < 4; round++)
        {
            var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true);
            await database.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = @sprintId", new { sprintId });
            var barrier = new TaskCompletionSource();
            var automatic = Task.Run(async () =>
            {
                await barrier.Task;
                await PublishClosedAsync(projectId, sprintId, SeedIds.Users.Joel);
            });
            var button = Task.Run(async () =>
            {
                await barrier.Task;
                return await DemoRunCalls.ButtonAsync(Api, sprintId);
            });
            barrier.SetResult();
            await automatic;
            using var pressed = await button;
            await _runs.WaitAsync(sprintId);

            (pressed.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict).ShouldBeTrue(pressed.StatusCode.ToString());
            var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
            (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem();
            (await UsableAsync(DemoRunArrange.TokenIn(await DemoRunCalls.DeliveredBodyAsync(database, itemId)))).ShouldBeTrue();
            (await DemoRunCalls.LiveTokensAsync(database, itemId)).ShouldBe(1);
            (await DemoRunCalls.TokensAsync(database, itemId)).ShouldBe(1);
            (await RequestOfAsync(database, sprintId)).Message.ShouldBeNull();
        }
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task CloseSprint_WhenPersonCloses_ShouldStartTheDemoAsTheCloser()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, _, _) = await ArrangeAsync(database, automatic: true);

        using var closed = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([]));
        closed.StatusCode.ShouldBe(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());
        await _runs.WaitAsync(sprintId);

        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem().CreatedBy.ShouldBe(SeedIds.Users.Joel);
        (await RequestOfAsync(database, sprintId)).ShouldSatisfyAllConditions(
            request => request.Via.ShouldBe("close"),
            request => request.By.ShouldBe(SeedIds.Users.Joel));
        (await UsableAsync(DemoRunArrange.TokenIn(await DemoRunCalls.DeliveredBodyAsync(database, itemId)))).ShouldBeTrue();
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task EndSprintTool_WhenApplied_ShouldStartTheDemo()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, _, key) = await ArrangeAsync(database, automatic: true);
        var agent = await TokenAsync(TestUser.Joel, ReadWritePlan);
        var arguments = $$"""{"project":"{{key}}","sprint":"{{await database.ExecuteScalarAsync<string>("SELECT name FROM sprint WHERE id = @sprintId", new { sprintId })}}"}""";
        var preview = await CallToolAsync(agent, "end_sprint", arguments);
        preview.IsToolError().ShouldBeFalse(preview.Body);

        var applied = await CallToolAsync(agent, "end_sprint", $$"""{"project":"{{key}}","plan":"{{preview.Structured().GetProperty("plan").GetString()}}"}""");
        applied.IsToolError().ShouldBeFalse(applied.Body);
        await _runs.WaitAsync(sprintId);

        var itemId = (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldNotBeNull();
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldHaveSingleItem().CreatedBy.ShouldBe(SeedIds.Users.Joel);
        (await RequestOfAsync(database, sprintId)).Via.ShouldBe("close");
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task CloseSprint_WhenTheRunThrows_ShouldStillSucceed()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, _, _) = await ArrangeAsync(database, automatic: true);
        _runs.Fail = true;

        using var closed = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([]));
        await _runs.WaitAsync(sprintId);

        closed.StatusCode.ShouldBe(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());
        (await database.ExecuteScalarAsync<string>("SELECT rollover_status FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe("decided");
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task Subscriber_WhenTheRunThrows_ShouldLogAndSwallowItself()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true);
        _runs.Fail = true;
        var captured = new CapturedSubscription();
        var subscriber = new ShipLean.Application.Features.Demos.SprintDemoAutomaticSubscriber(
            captured, Host.Services.GetRequiredService<IServiceScopeFactory>(), _logs.CreateLogger<ShipLean.Application.Features.Demos.SprintDemoAutomaticSubscriber>());
        await ((IHostedService)subscriber).StartAsync(CancellationToken.None);

        // Called directly, so the bus's own catch is not in the way: the handler must complete by itself.
        await Should.NotThrowAsync(() => captured.Handler.ShouldNotBeNull()(new SprintClosed(projectId, sprintId, SeedIds.Users.Kai)));

        _logs.Events.ShouldContain(e => e.StartsWith("Error ", StringComparison.Ordinal) && e.Contains("Could not start the automatic demo") && e.Contains(sprintId.ToString()));
    }

    [Test]
    [CancelAfter(120_000)]
    public async Task OtherEvents_ShouldNotStartADemo()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, _, _) = await ArrangeAsync(database, automatic: true);
        var bus = Host.Services.GetRequiredService<IProjectEvents>();
        var sentinelSprint = (await ArrangeAsync(database, automatic: true)).SprintId;

        await bus.PublishAsync(new SprintChanged(projectId, sprintId));
        await bus.PublishAsync(new SprintDemoChanged(projectId, sprintId));
        // The bus delivers in order: once the sentinel's close has run, the two events above have been handled.
        await PublishClosedAsync((await database.ExecuteScalarAsync<Guid>("SELECT project_id FROM sprint WHERE id = @sentinelSprint", new { sentinelSprint })), sentinelSprint, SeedIds.Users.Kai);
        await _runs.WaitAsync(sentinelSprint);

        (await RequestOfAsync(database, sprintId)).ShouldBe((null, null, null, null));
        (await DemoRunCalls.ItemOfAsync(database, sprintId)).ShouldBeNull();
        (await SessionsInProjectAsync(database, projectId)).ShouldBe(0);
    }
}
