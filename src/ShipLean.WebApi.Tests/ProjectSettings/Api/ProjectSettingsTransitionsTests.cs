using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.ProjectSettings.Api;

// A project-states save keeps the Stories board's transitions in step with the states it ends with: names refresh, a removed state
// leaves the transition Invalid, and a state with the same name (same type, any case) makes it valid again.
[TestFixture]
public class ProjectSettingsTransitionsTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<(ArrangedProject Project, Guid Active)> ArrangeAsync(WorkItemTypeId type = WorkItemTypeId.Story)
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin));
        var active = project.State(type, "Active");
        var transition = new TransitionJson("t1", type, new StateRefJson(project.State(type, "New"), "New"), new StateRefJson(active, "Active"), [new ActionJson("a1", "agent", "Do it", null)]);
        var settings = await ReadBoardAsync(connection, project.Id);
        await connection.ExecuteAsync(
            "UPDATE board SET settings = @settings::jsonb WHERE project_id = @id",
            new { id = project.Id, settings = JsonSerializer.Serialize(settings with { Transitions = [transition] }, PersistedJson.Options) });
        return (project, active);
    }

    private static async Task<BoardSettingsJson> ReadBoardAsync(System.Data.Common.DbConnection connection, Guid projectId)
        => JsonSerializer.Deserialize<BoardSettingsJson>(
            await connection.ExecuteScalarAsync<string>("SELECT settings::text FROM board WHERE project_id = @projectId", new { projectId }) ?? "", PersistedJson.Options)!;

    private async Task<TransitionJson> TransitionAsync(ArrangedProject project)
    {
        await using var connection = await OpenDatabaseAsync();
        return (await ReadBoardAsync(connection, project.Id)).Transitions.Single();
    }

    private async Task<bool> IsValidAsync(ArrangedProject project)
    {
        await using var connection = await OpenDatabaseAsync();
        var live = (await connection.QueryAsync<(Guid Id, string Type, string Name)>(
            "SELECT id, type, name FROM state WHERE project_id = @id AND archived_at IS NULL", new { id = project.Id }))
            .Select(s => new TransitionState(s.Id, Enum.Parse<WorkItemTypeId>(s.Type, true), s.Name, false)).ToList();
        return TransitionStates.IsValid(await TransitionAsync(project), live);
    }

    private async Task SaveAsync(ArrangedProject project, StateChanges changes)
    {
        var loaded = await Api.GetAsync<ProjectSettingsDto>($"api/projects/{project.Id}/settings");
        var request = new ProjectSettingsRequest(
            new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays),
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            changes);
        using var response = await Api.PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, WireOptions));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static StateChanges Rename(Guid state, string name) => new([], [new StateUpdate(state, name, StateCategory.InProgress)], [], []);

    private static StateChanges Remove(ArrangedProject project, WorkItemTypeId type, Guid state) => new([], [], [], [new StateRemove(state, project.State(type, "Resolved"))]);

    [Test]
    public async Task Settings_WhenAStateIsRenamed_ShouldRefreshTheTransitionName()
    {
        var (project, active) = await ArrangeAsync();

        await SaveAsync(project, Rename(active, "Doing"));

        var transition = await TransitionAsync(project);
        transition.To.ShouldBe(new StateRefJson(active, "Doing"));
        (await IsValidAsync(project)).ShouldBeTrue();
    }

    [Test]
    public async Task Settings_WhenAStateIsRemoved_ShouldKeepTheTransitionAsInvalid()
    {
        var (project, active) = await ArrangeAsync();

        await SaveAsync(project, Remove(project, WorkItemTypeId.Story, active));

        var transition = await TransitionAsync(project);
        transition.To.ShouldBe(new StateRefJson(active, "Active"));
        transition.Actions.Single().Prompt.ShouldBe("Do it");
        (await IsValidAsync(project)).ShouldBeFalse();
    }

    [TestCase("Active")]
    [TestCase("ACTIVE")]
    public async Task Settings_WhenAStateOfTheSameNameIsReAdded_ShouldBindTheTransitionToTheNewState(string name)
    {
        var (project, active) = await ArrangeAsync();
        await SaveAsync(project, Remove(project, WorkItemTypeId.Story, active));
        var fresh = Guid.NewGuid();

        await SaveAsync(project, new StateChanges([new StateAdd(fresh, WorkItemTypeId.Story, name, StateCategory.InProgress)], [], [], []));

        (await TransitionAsync(project)).To.StateId.ShouldBe(fresh);
        (await IsValidAsync(project)).ShouldBeTrue();
    }

    [Test]
    public async Task Settings_WhenAStateIsRemovedAndReAddedInOneSave_ShouldBindTheTransitionToTheNewState()
    {
        var (project, active) = await ArrangeAsync();
        var fresh = Guid.NewGuid();

        await SaveAsync(project, new StateChanges([new StateAdd(fresh, WorkItemTypeId.Story, "Active", StateCategory.InProgress)], [], [], [new StateRemove(active, fresh)]));

        (await TransitionAsync(project)).To.StateId.ShouldBe(fresh);
        (await IsValidAsync(project)).ShouldBeTrue();
    }

    [Test]
    public async Task Settings_WhenOnlyAnotherTypeHasAStateOfTheName_ShouldLeaveTheTransitionInvalid()
    {
        // Feature and Bug each have a live "Active" state of their own.
        var (project, active) = await ArrangeAsync();

        await SaveAsync(project, Remove(project, WorkItemTypeId.Story, active));

        (await TransitionAsync(project)).To.StateId.ShouldBe(active);
        (await IsValidAsync(project)).ShouldBeFalse();
    }

    [Test]
    public async Task Settings_WhenAnotherStateIsRenamedToTheMissingName_ShouldBindTheTransitionToIt()
    {
        var (project, active) = await ArrangeAsync();
        await SaveAsync(project, Remove(project, WorkItemTypeId.Story, active));
        var resolved = project.State(WorkItemTypeId.Story, "Resolved");

        await SaveAsync(project, new StateChanges([], [new StateUpdate(resolved, "Active", StateCategory.Resolved)], [], []));

        (await TransitionAsync(project)).To.ShouldBe(new StateRefJson(resolved, "Active"));
        (await IsValidAsync(project)).ShouldBeTrue();
    }

    [Test]
    public async Task Settings_WhenTransitionsChange_ShouldPublishBoardSettingsChangedOncePerTouchedBoard()
    {
        var (project, active) = await ArrangeAsync();
        var published = Channel.CreateUnbounded<BoardSettingsChanged>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(project.Id, e =>
        {
            if (e is BoardSettingsChanged changed)
            {
                published.Writer.TryWrite(changed);
            }
            return Task.CompletedTask;
        });

        await SaveAsync(project, Rename(active, "Doing"));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var changed = await published.Reader.ReadAsync(timeout.Token);
        changed.Sections.ShouldBe(["transitions"]);
        await SaveAsync(project, new StateChanges([], [], [], []));
        published.Reader.TryRead(out _).ShouldBeFalse();
    }
}
