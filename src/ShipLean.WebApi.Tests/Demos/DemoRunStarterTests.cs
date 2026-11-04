using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Demos;

// K6's start and decision over the real stores. The sprint data and the prompt are doubles (the prompt is only the upload link).
[TestFixture]
public class DemoRunStarterTests : ApiFixture
{
    private readonly UploadUrlPrompt _prompt = new();
    private BrandInspectionProbe _probe = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ISprintDemoData>();
        services.AddScoped<ISprintDemoData, SprintNamedDemoData>();
        services.RemoveAll<ISprintDemoPrompt>();
        services.AddSingleton<ISprintDemoPrompt>(_prompt);
        var real = services.Single(d => d.ServiceType == typeof(IDemoBrandSource));
        services.Remove(real);
        services.AddScoped(real.ImplementationType!);
        services.AddScoped<IDemoBrandSource>(sp => _probe = new BrandInspectionProbe(sp.GetRequiredService<NpgsqlDataSource>(), sp.GetRequiredService(real.ImplementationType!) as IDemoBrandSource ?? throw new InvalidOperationException()));
    };

    private IServiceScope NewScope() => Host.Services.CreateScope();

    private static IDemoRunStarter StarterOf(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IDemoRunStarter>();

    private async Task<(Guid ProjectId, Guid SprintId, Guid ItemId)> NewDemoAsync(NpgsqlConnection database)
    {
        using var scope = NewScope();
        return await DemoRunArrange.NewDemoAsync(database, Api, StarterOf(scope));
    }

    private static async Task<int> TokensAsync(NpgsqlConnection database, Guid itemId, string where = "")
        => await database.ExecuteScalarAsync<int>($"SELECT count(*) FROM transfer_token WHERE work_item_id = @itemId AND kind = 'demo' {where}", new { itemId });

    private async Task<bool> UsableAsync(string token)
    {
        using var scope = NewScope();
        return await scope.ServiceProvider.GetRequiredService<ITransferTokens>().FindAsync(TransferKind.Demo, token) is not null;
    }

    private static async Task<string> DeliveredTokenAsync(NpgsqlConnection database, Guid itemId)
        => DemoRunArrange.TokenIn((await AgentDispatchArrange.DeliveriesAsync(database, itemId)).Single().Body);

    [Test]
    public async Task Item_ShouldBeBauWithNoSprintOrParent()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, sprintId, itemId) = await NewDemoAsync(database);

        var row = await database.QuerySingleAsync<(string Type, Guid? SprintId, Guid? ParentId, string Title)>(
            "SELECT type, sprint_id, parent_id, title FROM work_item WHERE id = @itemId", new { itemId });
        row.ShouldBe(("bau", null, null, "Sprint 23 demo"));
        (await database.ExecuteScalarAsync<Guid?>("SELECT demo_work_item_id FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe(itemId);
    }

    [Test]
    public async Task Item_ShouldNameShipLeanInItsHistory()
    {
        await using var database = await OpenDatabaseAsync();
        var (_, _, itemId) = await NewDemoAsync(database);

        var revision = await database.QuerySingleAsync<(string? Webhook, Guid? ChangedBy)>(
            "SELECT webhook, changed_by FROM revision WHERE work_item_id = @itemId AND rev = 1", new { itemId });
        revision.ShouldBe(("ShipLean sprint demo", null));
    }

    [Test]
    public async Task Item_WhenConcurrent_ShouldMakeOne()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var sprintId = await DemoRunArrange.NewSprintAsync(database, arranged.Project.Id);
        var barrier = new TaskCompletionSource();

        var racers = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            await barrier.Task;
            using var scope = NewScope();
            return await StarterOf(scope).GetOrCreateItemAsync(arranged.Project.Id, sprintId, "Sprint 23", CancellationToken.None);
        })).ToList();
        barrier.SetResult();
        var ids = await Task.WhenAll(racers);

        ids.Distinct().ShouldHaveSingleItem();
        (await database.ExecuteScalarAsync<int>("SELECT count(*) FROM work_item WHERE project_id = @id AND type = 'bau'", new { id = arranged.Project.Id })).ShouldBe(1);
    }

    [Test]
    [CancelAfter(90_000)]
    public async Task Item_WhenCreating_ShouldTakeNoSprintRowLockAcrossTheStore()
    {
        await using var database = await OpenDatabaseAsync();
        for (var round = 0; round < 4; round++)
        {
            var (rolling, toSprint, toBacklog) = await SprintRolloverSupport.ArrangeEndedSprintAsync(database, $"Rolling {round}");
            var demoSprint = await DemoRunArrange.NewSprintAsync(database, SeedIds.Projects.Ship, $"Demo sprint {round}");
            var barrier = new TaskCompletionSource();
            var creating = Task.Run(async () =>
            {
                await barrier.Task;
                using var scope = NewScope();
                await StarterOf(scope).GetOrCreateItemAsync(SeedIds.Projects.Ship, demoSprint, $"Demo sprint {round}", CancellationToken.None);
            });
            var rolling2 = Task.Run(async () =>
            {
                await barrier.Task;
                return await Api.PostJsonAsync($"api/sprints/{rolling}/rollover", new ShipLean.Contracts.Sprints.RolloverRequest([new(toSprint, null), new(toBacklog, null)]));
            });
            barrier.SetResult();

            await creating;
            using var response = await rolling2;
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            (await database.ExecuteScalarAsync<Guid?>("SELECT demo_work_item_id FROM sprint WHERE id = @demoSprint", new { demoSprint })).ShouldNotBeNull();
        }
    }

    [Test]
    public async Task Session_ShouldIssueOneTokenAndSetTheRequestedFields()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        using var scope = NewScope();

        var outcome = await StarterOf(scope).StartSessionAsync(DemoRunArrange.Request(projectId, sprintId, itemId), CancellationToken.None);

        outcome.SessionId.ShouldNotBeNull();
        (await TokensAsync(database, itemId)).ShouldBe(1);
        (await UsableAsync(await DeliveredTokenAsync(database, itemId))).ShouldBeTrue();
        var sprint = await database.QuerySingleAsync<(Guid? By, string? Via, DateTimeOffset? At, DateTimeOffset? Expires, string? Blocked)>(
            "SELECT demo_requested_by, demo_requested_via, demo_requested_at, demo_upload_expires_at, demo_blocked_message FROM sprint WHERE id = @sprintId", new { sprintId });
        sprint.By.ShouldBe(SeedIds.Users.Joel);
        sprint.Via.ShouldBe("button");
        sprint.At.ShouldNotBeNull();
        sprint.Blocked.ShouldBeNull();
        var tokenExpires = await database.ExecuteScalarAsync<DateTimeOffset>("SELECT expires_at FROM transfer_token WHERE work_item_id = @itemId AND kind = 'demo'", new { itemId });
        sprint.Expires.ShouldBe(tokenExpires);
        (tokenExpires - sprint.At.Value).ShouldBeGreaterThan(TimeSpan.FromHours(23));
    }

    [Test]
    public async Task Session_WhenLost_ShouldIssueNothingAndWriteNothing()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        using var scope = NewScope();
        await scope.ServiceProvider.GetRequiredService<AgentSessionStarter>().StartAsync(itemId, SeedIds.Users.Joel, null, null, null, null, CancellationToken.None);

        var outcome = await StarterOf(scope).StartSessionAsync(DemoRunArrange.Request(projectId, sprintId, itemId), CancellationToken.None);

        outcome.Block.ShouldBe(AgentStartBlock.LiveSession);
        (await TokensAsync(database, itemId)).ShouldBe(0);
        (await database.ExecuteScalarAsync<DateTimeOffset?>("SELECT demo_requested_at FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBeNull();
        (await database.ExecuteScalarAsync<DateTimeOffset?>("SELECT demo_upload_expires_at FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBeNull();
    }

    [Test]
    public async Task Session_ShouldRevokeTheOldLinks()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        using var scope = NewScope();
        var old = await StarterOf(scope).HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None);
        var oldToken = DemoRunArrange.TokenIn(old.UploadUrl);
        (await UsableAsync(oldToken)).ShouldBeTrue();

        var outcome = await StarterOf(scope).StartSessionAsync(DemoRunArrange.Request(projectId, sprintId, itemId), CancellationToken.None);

        outcome.SessionId.ShouldNotBeNull();
        (await UsableAsync(oldToken)).ShouldBeFalse();
        (await UsableAsync(await DeliveredTokenAsync(database, itemId))).ShouldBeTrue();
        (await TokensAsync(database, itemId, "AND revoked_at IS NULL")).ShouldBe(1);
    }

    [Test]
    public async Task Session_WhenPromptFails_ShouldLeaveTheOldLinkLiveAndNoNewToken()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        using var scope = NewScope();
        var old = await StarterOf(scope).HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None);
        var oldToken = DemoRunArrange.TokenIn(old.UploadUrl);

        _prompt.Fail = true;
        try
        {
            await Should.ThrowAsync<InvalidOperationException>(() => StarterOf(scope).StartSessionAsync(DemoRunArrange.Request(projectId, sprintId, itemId), CancellationToken.None));
        }
        finally
        {
            _prompt.Fail = false;
        }

        (await UsableAsync(oldToken)).ShouldBeTrue();
        (await TokensAsync(database, itemId)).ShouldBe(1);
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldBeEmpty();
        (await database.ExecuteScalarAsync<string>("SELECT demo_requested_via FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe("mcp");
    }

    [Test]
    public async Task HandOver_WhenLiveSession_ShouldConflictAndKeepTheLink()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        using var scope = NewScope();
        await StarterOf(scope).StartSessionAsync(DemoRunArrange.Request(projectId, sprintId, itemId), CancellationToken.None);
        var sessionToken = await DeliveredTokenAsync(database, itemId);

        var failure = await Should.ThrowAsync<ApiException>(() => StarterOf(scope).HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None));

        failure.Code.ShouldBe(ApiErrorCodes.Conflict);
        (await UsableAsync(sessionToken)).ShouldBeTrue();
        (await TokensAsync(database, itemId)).ShouldBe(1);
    }

    [Test]
    public async Task HandOver_ShouldStartNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        using var scope = NewScope();

        var handover = await StarterOf(scope).HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None);

        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldBeEmpty();
        (await UsableAsync(DemoRunArrange.TokenIn(handover.UploadUrl))).ShouldBeTrue();
        handover.Instructions.ShouldBe($"Upload to {handover.UploadUrl}");
        (await database.ExecuteScalarAsync<string>("SELECT demo_requested_via FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe("mcp");
    }

    [Test]
    public async Task HandOver_WhenTheItemIsLockedBySomeoneElse_ShouldWaitForTheItemLock()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        var settings = new NpgsqlConnectionStringBuilder(ConnectionString) { Options = "-c lock_timeout=500ms" };
        await using var shortTimeout = NpgsqlDataSource.Create(settings);
        using var scope = NewScope();
        var services = scope.ServiceProvider;
        var starter = new DemoRunStarter(
            shortTimeout, services.GetRequiredService<IWorkItemStore>(), services.GetRequiredService<AgentSessionStarter>(),
            services.GetRequiredService<ISprintDemoData>(), services.GetRequiredService<ISprintDemoPrompt>(), services.GetRequiredService<IDemoBrandSource>(),
            services.GetRequiredService<ITransferTokens>(), services.GetRequiredService<IProjectEvents>(), services.GetRequiredService<AttachmentLimits>(), TimeProvider.System);
        await using var holder = await OpenDatabaseAsync();
        await using var holding = await holder.BeginTransactionAsync();
        await holder.ExecuteAsync("SELECT id FROM work_item WHERE id = @itemId FOR NO KEY UPDATE", new { itemId }, holding);

        var blocked = await Should.ThrowAsync<PostgresException>(() => starter.HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None));
        await holding.RollbackAsync();
        var handover = await starter.HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None);

        blocked.SqlState.ShouldBe(PostgresErrorCodes.LockNotAvailable);
        (await TokensAsync(database, itemId, "AND revoked_at IS NULL")).ShouldBe(1);
        (await UsableAsync(DemoRunArrange.TokenIn(handover.UploadUrl))).ShouldBeTrue();
    }

    [Test]
    public async Task HandOver_WhenConcurrent_ShouldLeaveOneLiveToken()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        var barrier = new TaskCompletionSource();

        var racers = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            await barrier.Task;
            using var scope = NewScope();
            return await StarterOf(scope).HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None);
        })).ToList();
        barrier.SetResult();
        var handovers = await Task.WhenAll(racers);

        (await TokensAsync(database, itemId, "AND revoked_at IS NULL")).ShouldBe(1);
        var usable = 0;
        foreach (var handover in handovers)
        {
            usable += await UsableAsync(DemoRunArrange.TokenIn(handover.UploadUrl)) ? 1 : 0;
        }

        usable.ShouldBe(1);
        (await AgentDispatchArrange.SessionsAsync(database, itemId)).ShouldBeEmpty();
    }

    [Test]
    public async Task Decision_ShouldPublishSprintDemoChangedAfterTheCommit()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        var events = new RecordedDemoEvents(Host.Services.GetRequiredService<NpgsqlDataSource>());
        using var scope = NewScope();
        var services = scope.ServiceProvider;
        var starter = new DemoRunStarter(
            services.GetRequiredService<NpgsqlDataSource>(), services.GetRequiredService<IWorkItemStore>(), services.GetRequiredService<AgentSessionStarter>(),
            services.GetRequiredService<ISprintDemoData>(), services.GetRequiredService<ISprintDemoPrompt>(), services.GetRequiredService<IDemoBrandSource>(),
            services.GetRequiredService<ITransferTokens>(), events, services.GetRequiredService<AttachmentLimits>(), TimeProvider.System);

        await starter.HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None);

        events.TokenVisibleAtPublish.ShouldBe(true);
    }

    [Test]
    public async Task Start_ShouldInspectTheBrandBeforeTheTransaction()
    {
        await using var database = await OpenDatabaseAsync();
        var (projectId, sprintId, itemId) = await NewDemoAsync(database);
        using var scope = NewScope();

        await StarterOf(scope).HandOverAsync(DemoRunArrange.Request(projectId, sprintId, itemId, "mcp"), CancellationToken.None);
        await StarterOf(scope).StartSessionAsync(DemoRunArrange.Request(projectId, sprintId, itemId), CancellationToken.None);

        _probe.TransactionOpen.Count.ShouldBeGreaterThanOrEqualTo(2);
        _probe.TransactionOpen.ShouldAllBe(open => !open);
    }
}
