using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Projects;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Sprints.Api;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// The trigger on real writes. The host's own subscriber is removed and the test subscribes the same AgentTrigger itself, so a
// test can wait for exactly the event it caused: the bus delivers in publish order and the probe signals after the trigger ran.
[TestFixture]
public class AgentTriggerTests : ApiFixture
{
    private readonly Channel<(ProjectEvent Event, Exception? Failure)> _handled = Channel.CreateUnbounded<(ProjectEvent, Exception?)>();
    private IDisposable _probe = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(AgentDispatchSubscriber)));

    [OneTimeSetUp]
    public void Probe()
    {
        var trigger = Host.Services.GetRequiredService<AgentTrigger>();
        _probe = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(null, async projectEvent =>
        {
            Exception? failure = null;
            try
            {
                await trigger.HandleAsync(projectEvent, CancellationToken.None);
            }
            catch (Exception failed)
            {
                failure = failed;
            }

            _handled.Writer.TryWrite((projectEvent, failure));
        });
    }

    [OneTimeTearDown]
    public void StopProbe() => _probe.Dispose();

    private async Task HandledAsync<T>(Guid workItemId) where T : ProjectEvent
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            var (projectEvent, failure) = await _handled.Reader.ReadAsync(timeout.Token);
            if (failure is not null)
            {
                throw failure;
            }

            if (projectEvent is T && Id(projectEvent) == workItemId)
            {
                return;
            }
        }
    }

    private static Guid Id(ProjectEvent projectEvent) => projectEvent switch
    {
        WorkItemCreated c => c.Id,
        WorkItemMoved m => m.Id,
        WorkItemUpdated u => u.Id,
        _ => Guid.Empty,
    };

    [Test]
    public async Task Move_WhenIntoAFlaggedState_ShouldOpenOneSessionAndQueueOneCreatedDelivery()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        await HandledAsync<WorkItemCreated>(item.Id);
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldBeEmpty();

        await AgentDispatchArrange.MoveAsync(Api, item.Id, item.Revision, arranged.Flagged);
        await HandledAsync<WorkItemUpdated>(item.Id);

        var session = (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        session.State.ShouldBe("submitted");
        session.ConnectionId.ShouldBe(arranged.ConnectionId!.Value);
        session.CreatedBy.ShouldBe(SeedIds.Users.Joel);
        session.RepoUrl.ShouldBe("https://github.com/ShipLean/ShipLean.git");
        session.BaseBranch.ShouldBe("develop");
        var delivery = (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem();
        (delivery.Action, delivery.Seq, delivery.Status, delivery.Attempts).ShouldBe(("created", 1, "pending", 0));
        var body = AgentDispatchArrange.BodyOf(delivery);
        body["action"]!.GetValue<string>().ShouldBe("created");
        body["type"]!.GetValue<string>().ShouldBe("AgentSessionEvent");
        body["appUserId"]!.GetValue<string>().ShouldBe(arranged.ConnectionId.Value.ToString());
        body["webhookTimestamp"].ShouldBeNull();
        body["webhookId"].ShouldBeNull();
        body["agentSession"]!["id"]!.GetValue<string>().ShouldBe(session.Id.ToString());
        body["agentSession"]!["creator"]!["email"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
        body["agentSession"]!["issue"]!["identifier"]!.GetValue<string>().ShouldBe(item.Key);
        body["agentSession"]!["issue"]!["url"]!.GetValue<string>().ShouldEndWith($"/p/{arranged.Project.Key}/items/{item.Key}");
        var prompt = body["promptContext"]!.GetValue<string>();
        prompt.ShouldContain("https://github.com/ShipLean/ShipLean.git");
        prompt.ShouldContain("develop");
    }

    [Test]
    public async Task Create_WhenIntoAFlaggedState_ShouldOpenOneSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);

        var item = await arranged.CreateItemAsync(Api, arranged.Flagged);
        await HandledAsync<WorkItemCreated>(item.Id);

        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem().State.ShouldBe("submitted");
        (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem().Action.ShouldBe("created");
    }

    [Test]
    public async Task Move_WhenIntoAnUnflaggedState_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);

        await AgentDispatchArrange.MoveAsync(Api, item.Id, item.Revision, arranged.Unflagged);
        await HandledAsync<WorkItemUpdated>(item.Id);

        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldBeEmpty();
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task Move_WhenTheProjectHasNoConnectionOrNoRepository_ShouldOpenNoSession(bool connection, bool repository)
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api, connection, repository);
        var item = await arranged.CreateItemAsync(Api);

        await AgentDispatchArrange.MoveAsync(Api, item.Id, item.Revision, arranged.Flagged);
        await HandledAsync<WorkItemUpdated>(item.Id);

        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldBeEmpty();
        (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Move_WhenTheConnectionIsArchived_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        await database.ExecuteAsync("UPDATE agent_connection SET archived_at = now() WHERE id = @id", new { id = arranged.ConnectionId });
        var item = await arranged.CreateItemAsync(Api);

        await AgentDispatchArrange.MoveAsync(Api, item.Id, item.Revision, arranged.Flagged);
        await HandledAsync<WorkItemUpdated>(item.Id);

        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Move_WhenASessionIsLive_ShouldNotOpenAnotherUntilItEnds()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var moved = await AgentDispatchArrange.MoveAsync(Api, item.Id, item.Revision, arranged.Flagged);
        await HandledAsync<WorkItemUpdated>(item.Id);
        var out1 = await AgentDispatchArrange.MoveAsync(Api, item.Id, moved.Revision, arranged.Unflagged);
        await HandledAsync<WorkItemUpdated>(item.Id);

        var in2 = await AgentDispatchArrange.MoveAsync(Api, item.Id, out1.Revision, arranged.Flagged);
        await HandledAsync<WorkItemUpdated>(item.Id);

        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem();
        await database.ExecuteAsync("UPDATE agent_session SET state = 'completed', ended_at = now() WHERE work_item_id = @id", new { id = item.Id });
        var out2 = await AgentDispatchArrange.MoveAsync(Api, item.Id, in2.Revision, arranged.Unflagged);
        await HandledAsync<WorkItemUpdated>(item.Id);

        await AgentDispatchArrange.MoveAsync(Api, item.Id, out2.Revision, arranged.Flagged);
        await HandledAsync<WorkItemUpdated>(item.Id);

        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Trigger_WhenTheRevisionIsAnImport_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await ArrangeItemInFlaggedStateAsync(database, arranged);
        var job = Guid.NewGuid();
        var jira = Guid.NewGuid();
        await database.ExecuteAsync(
            "INSERT INTO jira_connection (id, project_id, base_url, email, created_by) VALUES (@jira, @project, 'https://jira.example.com', 'j@example.com', @joel)",
            new { jira, project = arranged.Project.Id, joel = SeedIds.Users.Joel });
        await database.ExecuteAsync(
            "INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress) VALUES (@job, @connection, 'PLAT', 'EMPT', @project, 'running', '{}'::jsonb)",
            new { job, connection = jira, project = arranged.Project.Id });
        await new RevisionBuilder(item).WithChangedBy(SeedIds.Users.Joel).WithChanges(StateChangeTo(arranged.Flagged)).WithImport("jira", job).InsertAsync(database);

        await RunAsync(new WorkItemUpdated(arranged.Project.Id, item, 2, ["state"], false));

        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Trigger_WhenAnImportedItemIsCreated_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await ArrangeItemInFlaggedStateAsync(database, arranged);
        await database.ExecuteAsync("UPDATE work_item SET original_system = 'jira', original_system_id = 'ABC-1' WHERE id = @item", new { item });

        await RunAsync(new WorkItemCreated(arranged.Project.Id, item, "X-1", WorkItemTypeId.Story, null));

        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Trigger_WhenTheRevisionIsAWebhookWithNoPerson_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await ArrangeItemInFlaggedStateAsync(database, arranged);
        await new RevisionBuilder(item).WithChangedBy(null).WithChanges(StateChangeTo(arranged.Flagged)).InsertAsync(database);

        await RunAsync(new WorkItemMoved(arranged.Project.Id, item, 2, null, false, null, null, arranged.Flagged, null));

        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Trigger_WhenTheRevisionDidNotChangeTheState_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await ArrangeItemInFlaggedStateAsync(database, arranged);
        await new RevisionBuilder(item).WithChangedBy(SeedIds.Users.Joel).WithChanges("""[{"field":"title","from":"a","to":"b"}]""").InsertAsync(database);

        await RunAsync(new WorkItemUpdated(arranged.Project.Id, item, 2, ["title"], false));

        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Trigger_WhenTheStateChangeIsOld_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await ArrangeItemInFlaggedStateAsync(database, arranged);
        await new RevisionBuilder(item).WithChangedBy(SeedIds.Users.Joel).WithChanges(StateChangeTo(arranged.Flagged)).WithChangedAt(TimeProvider.System.GetUtcNow().AddHours(-1)).InsertAsync(database);

        await RunAsync(new WorkItemMoved(arranged.Project.Id, item, 2, null, false, null, null, arranged.Flagged, null));

        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Trigger_WhenAPersonChangedTheStateIntoAFlaggedOne_ShouldOpenASessionForThem()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await ArrangeItemInFlaggedStateAsync(database, arranged);
        await new RevisionBuilder(item).WithChangedBy(SeedIds.Users.Priya).WithChanges(StateChangeTo(arranged.Flagged)).InsertAsync(database);

        await RunAsync(new WorkItemUpdated(arranged.Project.Id, item, 2, ["state"], false));

        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldHaveSingleItem().CreatedBy.ShouldBe(SeedIds.Users.Priya);
    }

    [Test]
    public async Task Trigger_WhenTheRevisionMovedTheItemIntoAnUnflaggedStateButItIsFlaggedNow_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await ArrangeItemInFlaggedStateAsync(database, arranged);
        await new RevisionBuilder(item).WithChangedBy(SeedIds.Users.Joel).WithChanges(StateChangeTo(arranged.Unflagged)).InsertAsync(database);

        await RunAsync(new WorkItemUpdated(arranged.Project.Id, item, 2, ["state"], false));

        (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Trigger_WhenAnAgentOrABulkWriteOrAWebhookMovedTheItem_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        foreach (var arrange in new Func<RevisionBuilder, RevisionBuilder>[] { r => r.WithAgent("Claude Code (claude.ai)"), r => r.WithBulk(), r => r.WithWebhook("GitHub acme/api#12") })
        {
            var item = await ArrangeItemInFlaggedStateAsync(database, arranged);
            await arrange(new RevisionBuilder(item).WithChangedBy(SeedIds.Users.Joel).WithChanges(StateChangeTo(arranged.Flagged))).InsertAsync(database);

            await RunAsync(new WorkItemUpdated(arranged.Project.Id, item, 2, ["state"], false));

            (await AgentDispatchArrange.SessionsAsync(database, item)).ShouldBeEmpty();
        }
    }

    [Test]
    public async Task Settings_WhenRemovingAStateRemapsItemsIntoAFlaggedOne_ShouldOpenNoSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api, arranged.Unflagged);
        await HandledAsync<WorkItemCreated>(item.Id);
        var wire = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        var loaded = await Api.GetAsync<ProjectSettingsDto>($"api/projects/{arranged.Project.Id}/settings");
        var request = new ProjectSettingsRequest(
            new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays),
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            new StateChanges([], [], [], [new StateRemove(arranged.Unflagged, arranged.Flagged)]));

        using var response = await Api.PutJsonAsync($"api/projects/{arranged.Project.Id}/settings", JsonSerializer.SerializeToElement(request, wire));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await HandledAsync<WorkItemUpdated>(item.Id);

        (await database.ExecuteScalarAsync<Guid>("SELECT state_id FROM work_item WHERE id = @id", new { id = item.Id })).ShouldBe(arranged.Flagged);
        (await database.ExecuteScalarAsync<bool>("SELECT bulk FROM revision WHERE work_item_id = @id AND rev = 2", new { id = item.Id })).ShouldBeTrue();
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldBeEmpty();
    }

    private static string StateChangeTo(Guid stateId) => $$"""[{"field":"state","from":"{{Guid.NewGuid()}}","to":"{{stateId}}"}]""";

    private static Task<Guid> ArrangeItemInFlaggedStateAsync(Npgsql.NpgsqlConnection database, AgentDispatchArrange arranged)
        => new WorkItemBuilder(arranged.Project.Id).WithState(arranged.Flagged).InsertAsync(database);

    private Task RunAsync(ProjectEvent projectEvent) => Host.Services.GetRequiredService<AgentTrigger>().HandleAsync(projectEvent, CancellationToken.None);
}
