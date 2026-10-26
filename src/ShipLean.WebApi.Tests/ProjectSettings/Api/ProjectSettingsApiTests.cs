using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.ProjectSettings.Api;

// One seeded scenario for the settings page. Every test arranges a project of its own, with Joel (the signed-in user) as admin and Priya as a member.
[TestFixture]
public class ProjectSettingsApiTests : ApiFixture
{
    private const string JoelEmail = "joel@shiplean.local";
    private const string PriyaEmail = "priya@shiplean.local";
    private const string TomEmail = "tom@shiplean.local";

    // Enums cross the wire as camelCase strings; the shared client sends numbers.
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin), (SeedIds.Users.Priya, ProjectRole.Member));
    }

    private Task<ProjectSettingsDto> LoadAsync(ArrangedProject project) => Api.GetAsync<ProjectSettingsDto>($"api/projects/{project.Id}/settings");

    // The page as loaded, changed by the test, as it would be saved.
    private static ProjectSettingsRequest Request(ProjectSettingsDto loaded, Func<ProjectSettingsRequest, ProjectSettingsRequest>? change = null)
    {
        var request = new ProjectSettingsRequest(
            new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays),
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            new StateChanges([], [], [], []));
        return change is null ? request : change(request);
    }

    private Task<HttpResponseMessage> PutAsync(ArrangedProject project, ProjectSettingsRequest request) => Api.PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, WireOptions));

    private static string Wire(ProjectSettingsDto settings) => JsonSerializer.Serialize(settings, JsonSerializerOptions.Web);

    private static async Task ShouldBeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
        => (await ApiClient.ReadApiErrorAsync(response, status)).Code.ShouldBe(code);

    [Test]
    public async Task Settings_WhenLoadedAndSavedUnchanged_ShouldRoundTrip()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Wire(await LoadAsync(project)).ShouldBe(Wire(loaded));
        loaded.Members.Select(m => m.Email).ShouldBe([JoelEmail, PriyaEmail], ignoreOrder: true);
        loaded.States.Count.ShouldBe(25);
    }

    [Test]
    public async Task Settings_WhenKeyChanged_ShouldBeValidation()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded, r => r with { General = r.General with { Key = "ZZZZ", Name = "Renamed" } }));

        await ShouldBeAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.Validation);
        Wire(await LoadAsync(project)).ShouldBe(Wire(loaded));
    }

    [Test]
    public async Task Settings_WhenLastAdminDemoted_ShouldBeValidation()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded, r => r with { Members = [new MemberPut(JoelEmail, ProjectRole.Member), new MemberPut(PriyaEmail, ProjectRole.Member)] }));

        await ShouldBeAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.Validation);
        (await LoadAsync(project)).Members.ShouldContain(m => m.Role == ProjectRole.Admin);
    }

    [Test]
    public async Task Settings_WhenLastProposedStateDeleted_ShouldBeValidation()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);
        var proposed = project.State(WorkItemTypeId.Story, "New");
        var other = project.State(WorkItemTypeId.Story, "Active");

        using var response = await PutAsync(project, Request(loaded, r => r with { States = new StateChanges([], [], [], [new StateRemove(proposed, other)]) }));

        await ShouldBeAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.Validation);
        (await LoadAsync(project)).States.ShouldContain(s => s.Id == proposed);
    }

    [Test]
    public async Task Settings_WhenStateDeletedWithItems_ShouldMoveInSameTransactionAndRepairMappings()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var target = project.State(WorkItemTypeId.Story, "New");
        Guid item;
        await using (var connection = await OpenDatabaseAsync())
        {
            item = await new WorkItemBuilder(project.Id).WithState(active).InsertAsync(connection);
        }

        var loaded = await LoadAsync(project);
        loaded.States.Single(s => s.Id == active).ItemCount.ShouldBe(1);

        // The Active state is removed with New as its target, and the General name is saved in the same request.
        using var response = await PutAsync(project, Request(loaded, r => r with { General = r.General with { Name = "Saved together" }, States = new StateChanges([], [], [], [new StateRemove(active, target)]) }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await using var check = await OpenDatabaseAsync();
        var row = await check.QuerySingleAsync<(Guid StateId, int Revision)>("SELECT state_id, revision FROM work_item WHERE id = @item", new { item });
        row.StateId.ShouldBe(target);
        row.Revision.ShouldBe(2);
        (await check.ExecuteScalarAsync<bool>("SELECT archived_at IS NOT NULL FROM state WHERE id = @active", new { active })).ShouldBeTrue();
        var board = JsonSerializer.Deserialize<BoardSettingsJson>(await check.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id", new { id = project.Id }) ?? "", PersistedJson.Options)!;
        board.Columns.SelectMany(c => c.StateMappings).ShouldNotContain(m => m.StateId == active);
        board.Columns.Single(c => c.Id == "col-doing").StateMappings.ShouldContain(m => m.Type == WorkItemTypeId.Story && m.StateId == target);
        var after = await LoadAsync(project);
        after.States.ShouldNotContain(s => s.Id == active);
        after.States.Single(s => s.Id == target).ItemCount.ShouldBe(1);
        after.General.Name.ShouldBe("Saved together");
    }

    [Test]
    public async Task Settings_WhenStateDeletedIntoStateAddedInSameSave_ShouldMoveItemsToTheNewState()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        Guid item;
        await using (var connection = await OpenDatabaseAsync())
        {
            item = await new WorkItemBuilder(project.Id).WithState(active).InsertAsync(connection);
        }

        var fresh = Guid.NewGuid();
        var changes = new StateChanges([new StateAdd(fresh, WorkItemTypeId.Story, "Doing", StateCategory.InProgress)], [], [], [new StateRemove(active, fresh)]);

        using var response = await PutAsync(project, Request(await LoadAsync(project), r => r with { States = changes }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await using var check = await OpenDatabaseAsync();
        var row = await check.QuerySingleAsync<(Guid StateId, int Revision)>("SELECT state_id, revision FROM work_item WHERE id = @item", new { item });
        row.StateId.ShouldBe(fresh);
        row.Revision.ShouldBe(2);
        var after = await LoadAsync(project);
        after.States.ShouldNotContain(s => s.Id == active);
        after.States.Single(s => s.Id == fresh).ItemCount.ShouldBe(1);
    }

    [Test]
    public async Task Settings_WhenEmptyStateDeletedIntoStateAddedInSameSave_ShouldSave()
    {
        var project = await ArrangeAsync();
        var resolved = project.State(WorkItemTypeId.Story, "Resolved");
        var fresh = Guid.NewGuid();
        var changes = new StateChanges([new StateAdd(fresh, WorkItemTypeId.Story, "Intake", StateCategory.Proposed)], [], [], [new StateRemove(resolved, fresh)]);

        using var response = await PutAsync(project, Request(await LoadAsync(project), r => r with { States = changes }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await LoadAsync(project)).States.ShouldContain(s => s.Id == fresh);
    }

    [Test]
    public async Task Settings_WhenTargetIsAddedStateOfAnotherType_ShouldBeValidationAndAddNothing()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var fresh = Guid.NewGuid();
        var changes = new StateChanges([new StateAdd(fresh, WorkItemTypeId.Bug, "Doing", StateCategory.InProgress)], [], [], [new StateRemove(active, fresh)]);

        using var response = await PutAsync(project, Request(await LoadAsync(project), r => r with { States = changes }));

        await ShouldBeAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.Validation);
        (await LoadAsync(project)).States.ShouldNotContain(s => s.Id == fresh);
    }

    [Test]
    public async Task Settings_WhenSaveFailsAfterAddingTheTargetState_ShouldLeaveNoNewState()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var resolved = project.State(WorkItemTypeId.Story, "Resolved");
        var fresh = Guid.NewGuid();
        var changes = new StateChanges(
            [new StateAdd(fresh, WorkItemTypeId.Story, "Doing", StateCategory.InProgress)], [new StateUpdate(resolved, " ", StateCategory.Resolved)], [], [new StateRemove(active, fresh)]);

        using var response = await PutAsync(project, Request(await LoadAsync(project), r => r with { States = changes }));

        await ShouldBeAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.Validation);
        var after = await LoadAsync(project);
        after.States.ShouldNotContain(s => s.Id == fresh);
        after.States.ShouldContain(s => s.Id == active);
    }

    [Test]
    public async Task Settings_WhenStateDeleted_ShouldRemapStateClausesInLaneAndStyleCriteria()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        var target = project.State(WorkItemTypeId.Story, "New");
        var other = project.State(WorkItemTypeId.Story, "Closed");
        await using (var connection = await OpenDatabaseAsync())
        {
            var text = await connection.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id", new { id = project.Id }) ?? "";
            var settings = JsonSerializer.Deserialize<BoardSettingsJson>(text, PersistedJson.Options)!;
            CriteriaJson Equals(Guid id) => new(CriteriaMatch.All, [new(FieldIds.State, CriteriaOp.Equal, JsonSerializer.SerializeToElement(id.ToString()))]);
            CriteriaJson In(Guid a, Guid b) => new(CriteriaMatch.Any, [new(FieldIds.State, CriteriaOp.In, JsonSerializer.SerializeToElement(new[] { a.ToString(), b.ToString() }))]);
            settings = settings with
            {
                Swimlanes = [.. settings.Swimlanes, new SwimlaneJson("lane-active", "Active work", null, SwimlaneKind.Criteria, Equals(active))],
                StyleRules = [new StyleRuleJson("rule-1", "Open", true, null, true, false, false, In(active, other))],
            };
            await connection.ExecuteAsync("UPDATE board SET settings = @settings::jsonb WHERE project_id = @id", new { id = project.Id, settings = JsonSerializer.Serialize(settings, PersistedJson.Options) });
        }

        var loaded = await LoadAsync(project);
        using var response = await PutAsync(project, Request(loaded, r => r with { States = new StateChanges([], [], [], [new StateRemove(active, target)]) }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await using var check = await OpenDatabaseAsync();
        var board = JsonSerializer.Deserialize<BoardSettingsJson>(await check.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @id", new { id = project.Id }) ?? "", PersistedJson.Options)!;
        board.Swimlanes.Single(l => l.Id == "lane-active").Criteria!.Clauses.Single().Value.GetString().ShouldBe(target.ToString());
        board.StyleRules.Single().Criteria.Clauses.Single().Value.EnumerateArray().Select(e => e.GetString()).ShouldBe([target.ToString(), other.ToString()]);
    }

    [Test]
    public async Task Settings_WhenOneSectionInvalid_ShouldChangeNothing()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        Guid item;
        await using (var connection = await OpenDatabaseAsync())
        {
            item = await new WorkItemBuilder(project.Id).WithState(active).InsertAsync(connection);
        }

        var loaded = await LoadAsync(project);
        var states = new StateChanges(
            [new StateAdd(Guid.NewGuid(), WorkItemTypeId.Story, "New", StateCategory.Proposed)], [], [], [new StateRemove(active, project.State(WorkItemTypeId.Story, "New"))]);

        using var response = await PutAsync(project, Request(loaded, r => r with
        {
            General = r.General with { Name = "Never saved" },
            Members = [new MemberPut(JoelEmail, ProjectRole.Admin), new MemberPut(TomEmail, ProjectRole.Member)],
            States = states,
        }));

        await ShouldBeAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.Validation);
        Wire(await LoadAsync(project)).ShouldBe(Wire(loaded));
        await using var check = await OpenDatabaseAsync();
        var row = await check.QuerySingleAsync<(Guid StateId, int Revision)>("SELECT state_id, revision FROM work_item WHERE id = @item", new { item });
        (row.StateId, row.Revision).ShouldBe((active, 1));
    }

    [Test]
    public async Task Members_WhenAddedByPartialEmail_ShouldBeNotFound()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded, r => r with { Members = [.. r.Members, new MemberPut("tom@shiplean", ProjectRole.Member)] }));

        await ShouldBeAsync(response, HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
        (await LoadAsync(project)).Members.Count.ShouldBe(2);
    }

    [Test]
    public async Task Members_WhenRemoved_ShouldBeArchivedNotDeleted()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);

        using var response = await PutAsync(project, Request(loaded, r => r with { Members = [.. r.Members.Where(m => m.Email != PriyaEmail), new MemberPut(TomEmail.ToUpperInvariant(), ProjectRole.Member)] }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await LoadAsync(project)).Members.Select(m => m.Email).ShouldBe([JoelEmail, TomEmail], ignoreOrder: true);
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<bool>(
            "SELECT archived_at IS NOT NULL FROM project_member WHERE project_id = @id AND user_id = @user", new { id = project.Id, user = SeedIds.Users.Priya })).ShouldBeTrue();

        using var readded = await PutAsync(project, Request(await LoadAsync(project), r => r with { Members = [.. r.Members, new MemberPut(PriyaEmail, ProjectRole.Admin)] }));
        readded.StatusCode.ShouldBe(HttpStatusCode.OK, await readded.Content.ReadAsStringAsync());
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project_member WHERE project_id = @id AND user_id = @user", new { id = project.Id, user = SeedIds.Users.Priya })).ShouldBe(1);
    }

    [Test]
    public async Task Leave_WhenOnlyAdmin_ShouldBeValidation()
    {
        var project = await ArrangeAsync();

        using var response = await Api.PostJsonAsync($"api/projects/{project.Id}/leave");

        await ShouldBeAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.Validation);
        (await LoadAsync(project)).Members.Count.ShouldBe(2);
    }

    [Test]
    public async Task Leave_WhenMember_ShouldArchiveTheirMembership()
    {
        var project = await ArrangeAsync();

        using var response = await Api.ActingAs(TestUser.Priya).PostJsonAsync($"api/projects/{project.Id}/leave");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoadAsync(project)).Members.Select(m => m.User.Id).ShouldBe([SeedIds.Users.Joel]);
        using var after = await Api.ActingAs(TestUser.Priya).GetAsync($"api/projects/{project.Id}/settings");
        await ShouldBeAsync(after, HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Settings_WhenSaved_ShouldPublishStatesChangedAndProjectChanged()
    {
        var project = await ArrangeAsync();
        var loaded = await LoadAsync(project);
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), project.Id);

        using var response = await PutAsync(project, Request(loaded, r => r with { States = new StateChanges([new StateAdd(Guid.NewGuid(), WorkItemTypeId.Bug, "Triaged", StateCategory.Proposed)], [], [], []) }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await watch.NextAsync<ProjectChanged>(e => e.ProjectId == project.Id)).ClientId.ShouldBe(ApiClient.ClientId);
        (await watch.NextAsync<StatesChanged>(e => e.ProjectId == project.Id)).ProjectId.ShouldBe(project.Id);
    }
}
