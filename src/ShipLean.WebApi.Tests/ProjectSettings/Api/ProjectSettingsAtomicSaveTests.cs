using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.ProjectSettings.Api;

// A settings save is atomic: states added in the save are inserted inside the work item store's transaction, so a failed
// save writes nothing and the same body can be sent again. The store is wrapped (ConfigureTestServices) so a test can write
// between the plan and the store's transaction, where a concurrent write would land.
[TestFixture]
public class ProjectSettingsAtomicSaveTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static Func<Task>? _beforePatchMany;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddScoped<IWorkItemStore>(sp =>
        {
            var proxy = DispatchProxy.Create<IWorkItemStore, BeforePatchManyProxy>();
            ((BeforePatchManyProxy)(object)proxy).Inner = ActivatorUtilities.CreateInstance<WorkItemStore>(sp);
            return proxy;
        });

    [TearDown]
    public void ClearHook() => _beforePatchMany = null;

    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin));
    }

    private async Task<ProjectSettingsRequest> RequestAsync(ArrangedProject project, StateChanges changes)
    {
        var loaded = await Api.GetAsync<ProjectSettingsDto>($"api/projects/{project.Id}/settings");
        return new ProjectSettingsRequest(
            new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays),
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            changes);
    }

    private Task<HttpResponseMessage> PutAsync(ArrangedProject project, ProjectSettingsRequest request)
        => Api.PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, WireOptions));

    // a later validation error, the user fixes the field and presses Save again with the same draft (same add id).
    [Test]
    public async Task Settings_WhenALaterValidationFailsAfterAddingTheTarget_ShouldSaveTheSameBodyOnceFixed()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var resolved = project.State(WorkItemTypeId.Story, "Resolved");
        var fresh = Guid.NewGuid();
        var add = new StateAdd(fresh, WorkItemTypeId.Story, "Doing", StateCategory.InProgress);
        using var first = await PutAsync(project, await RequestAsync(project, new StateChanges([add], [new StateUpdate(resolved, " ", StateCategory.Resolved)], [], [new StateRemove(active, fresh)])));
        first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using (var connection = await OpenDatabaseAsync())
        {
            (await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM state WHERE id = @fresh", new { fresh })).ShouldBe(0);
        }

        using var retry = await PutAsync(project, await RequestAsync(project, new StateChanges([add], [new StateUpdate(resolved, "Review", StateCategory.Resolved)], [], [new StateRemove(active, fresh)])));
        var body = await retry.Content.ReadAsStringAsync();
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }

    // a concurrent item write lands in the removed state (409), then the user retries the same draft.
    [Test]
    public async Task Settings_WhenALateItemConflictsAfterAddingTheTarget_ShouldSaveTheSameBodyOnRetry()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var fresh = Guid.NewGuid();
        var changes = new StateChanges([new StateAdd(fresh, WorkItemTypeId.Story, "Doing", StateCategory.InProgress)], [], [], [new StateRemove(active, fresh)]);
        var request = await RequestAsync(project, changes);
        var fired = false;
        _beforePatchMany = async () =>
        {
            if (fired)
            {
                return;
            }

            fired = true;
            await using var other = await OpenDatabaseAsync();
            await new WorkItemBuilder(project.Id).WithState(active).InsertAsync(other);
        };

        using var first = await PutAsync(project, request);
        first.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await using (var connection = await OpenDatabaseAsync())
        {
            (await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM state WHERE id = @fresh", new { fresh })).ShouldBe(0);
        }

        using var retry = await PutAsync(project, await RequestAsync(project, changes));
        var body = await retry.Content.ReadAsStringAsync();
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }

    // another user tries to move an item into the new state while a save that adds it fails: no item may end in a state no read shows.
    [Test]
    public async Task Settings_WhenAnotherUserMovesAnItemDuringAFailedSave_ShouldNeverSeeTheNewState()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var newState = project.State(WorkItemTypeId.Story, "New");
        Guid bystander;
        await using (var connection = await OpenDatabaseAsync())
        {
            bystander = await new WorkItemBuilder(project.Id).WithState(newState).InsertAsync(connection);
        }

        var fresh = Guid.NewGuid();
        var changes = new StateChanges([new StateAdd(fresh, WorkItemTypeId.Story, "Doing", StateCategory.InProgress)], [], [], [new StateRemove(active, fresh)]);
        var request = await RequestAsync(project, changes);
        _beforePatchMany = async () =>
        {
            var body = new JsonObject { ["expectedRevision"] = 1, ["changes"] = new JsonObject { ["state"] = new JsonObject { ["value"] = fresh.ToString() } } };
            using var moved = await Api.PatchJsonAsync($"api/work-items/{bystander}", body);
            moved.IsSuccessStatusCode.ShouldBeFalse();
            await using var other = await OpenDatabaseAsync();
            await new WorkItemBuilder(project.Id).WithState(active).InsertAsync(other);
        };

        using var response = await PutAsync(project, request);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await using var check = await OpenDatabaseAsync();
        var row = await check.QuerySingleAsync<(Guid StateId, bool Archived)>(
            "SELECT w.state_id, s.archived_at IS NOT NULL FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @bystander", new { bystander });
        row.Archived.ShouldBeFalse();
        row.StateId.ShouldBe(newState);
    }

    // a board save lands between the plan and the states transaction, naming a state by its old name: the states save normalises it.
    [Test]
    public async Task Settings_WhenABoardSaveRacesAStatesSave_ShouldLeaveTransitionsWithCurrentNames()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var request = await RequestAsync(project, new StateChanges([], [new StateUpdate(active, "Doing", StateCategory.InProgress)], [], []));
        _beforePatchMany = async () =>
        {
            Guid boardId;
            await using (var other = await OpenDatabaseAsync())
            {
                boardId = await other.ExecuteScalarAsync<Guid>("SELECT id FROM board WHERE project_id = @id AND level = 'stories'", new { id = project.Id });
            }

            using var got = await Api.GetAsync($"api/boards/{boardId}/settings");
            var board = JsonSerializer.Deserialize<BoardSettingsDto>(await got.Content.ReadAsStringAsync(), PersistedJson.Options)!;
            var transition = new TransitionJson("t1", WorkItemTypeId.Story, null, new StateRefJson(active, "Active"), [new ActionJson("a1", "agent", "Go", null)]);
            using var saved = await Api.PutJsonAsync($"api/boards/{board.Id}/settings", JsonSerializer.SerializeToNode(new BoardSettingsPut(board.Settings, board.Settings with { Transitions = [transition] }), PersistedJson.Options));
            saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        };

        using var response = await PutAsync(project, request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await using var check = await OpenDatabaseAsync();
        var settings = JsonSerializer.Deserialize<BoardSettingsJson>(await check.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id", new { id = project.Id }) ?? "", PersistedJson.Options)!;
        settings.Transitions.Single().To.ShouldBe(new StateRefJson(active, "Doing"));
    }

    // delete Active and re-add "Active", items going to the new one (one ApplyAsync accepts this).
    // An SPA cached from before the deploy still sends v1's startsAgent flag on state adds and updates; it is ignored, not refused.
    [Test]
    public async Task Settings_WhenAnOldClientSendsStartsAgentOnAStateAddAndUpdate_ShouldSave()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var changes = new StateChanges([new StateAdd(Guid.NewGuid(), WorkItemTypeId.Story, "Reviewing", StateCategory.InProgress)], [new StateUpdate(active, "Doing", StateCategory.InProgress)], [], []);
        var body = JsonSerializer.SerializeToNode(await RequestAsync(project, changes), WireOptions)!;
        body["states"]!["add"]![0]!["startsAgent"] = true;
        body["states"]!["update"]![0]!["startsAgent"] = true;

        using var response = await Api.PutJsonAsync($"api/projects/{project.Id}/settings", body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await LiveStatesAsync(project)).ShouldContain(s => s.Id == active && s.Name == "Doing");
    }

    [Test]
    public async Task Settings_WhenStateDeletedIntoNewStateOfTheSameName_ShouldSave()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var fresh = Guid.NewGuid();
        using var response = await PutAsync(project, await RequestAsync(project, new StateChanges([new StateAdd(fresh, WorkItemTypeId.Story, "Active", StateCategory.InProgress)], [], [], [new StateRemove(active, fresh)])));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        (await LiveStatesAsync(project)).ShouldContain(s => s.Id == fresh && s.Name == "Active");
    }

    // rename Active away, add a new "Active", delete the old one into it.
    [Test]
    public async Task Settings_WhenStateRenamedAwayAndReplacedByNewStateOfItsName_ShouldSave()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var fresh = Guid.NewGuid();
        using var response = await PutAsync(project, await RequestAsync(project, new StateChanges(
            [new StateAdd(fresh, WorkItemTypeId.Story, "Active", StateCategory.InProgress)], [new StateUpdate(active, "Active (old)", StateCategory.InProgress)], [], [new StateRemove(active, fresh)])));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        (await LiveStatesAsync(project)).ShouldContain(s => s.Id == fresh && s.Name == "Active");
    }

    // two admins split Active at the same time (different names), repeated: no 500/deadlock, no stray live state.
    [Test]
    public async Task Settings_WhenTwoAdminsSplitAStateAtOnce_ShouldLeaveOneLiveNewState()
    {
        for (var i = 0; i < 8; i++)
        {
            var project = await ArrangeAsync();
            var active = project.State(WorkItemTypeId.Story, "Active");
            await using (var connection = await OpenDatabaseAsync())
            {
                await new WorkItemBuilder(project.Id).WithState(active).InsertAsync(connection);
            }

            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var requestA = await RequestAsync(project, new StateChanges([new StateAdd(a, WorkItemTypeId.Story, "Doing A", StateCategory.InProgress)], [], [], [new StateRemove(active, a)]));
            var requestB = await RequestAsync(project, new StateChanges([new StateAdd(b, WorkItemTypeId.Story, "Doing B", StateCategory.InProgress)], [], [], [new StateRemove(active, b)]));
            var responses = await Task.WhenAll(PutAsync(project, requestA), PutAsync(project, requestB));
            await using var check = await OpenDatabaseAsync();
            var live = await check.ExecuteScalarAsync<long>("SELECT count(*) FROM state WHERE id = ANY(@ids) AND archived_at IS NULL", new { ids = new[] { a, b } });
            var orphans = await check.ExecuteScalarAsync<long>("SELECT count(*) FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.project_id = @p AND s.archived_at IS NOT NULL", new { p = project.Id });
            responses.ShouldNotContain(r => (int)r.StatusCode >= 500);
            live.ShouldBe(1);
            orphans.ShouldBe(0);
        }
    }

    // a target added for another type: is anything ever inserted (row, even archived)?
    [Test]
    public async Task Settings_WhenTargetIsAddedStateOfAnotherType_ShouldInsertNoStateRow()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var fresh = Guid.NewGuid();
        using var response = await PutAsync(project, await RequestAsync(project, new StateChanges([new StateAdd(fresh, WorkItemTypeId.Bug, "Doing", StateCategory.InProgress)], [], [], [new StateRemove(active, fresh)])));
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<long>("SELECT count(*) FROM state WHERE id = @fresh", new { fresh })).ShouldBe(0);
    }

    private async Task<IReadOnlyList<StateDto>> LiveStatesAsync(ArrangedProject project)
        => (await Api.GetAsync<ProjectSettingsDto>($"api/projects/{project.Id}/settings")).States;

    public class BeforePatchManyProxy : DispatchProxy
    {
        public IWorkItemStore Inner { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IWorkItemStore.PatchManyAsync) && _beforePatchMany is { } hook)
            {
                return Run(hook, targetMethod, args);
            }

            return targetMethod.Invoke(Inner, args);
        }

        private async Task<WriteManyOutcome> Run(Func<Task> hook, MethodInfo method, object?[]? args)
        {
            await hook();
            return await (Task<WriteManyOutcome>)method.Invoke(Inner, args)!;
        }
    }
}
