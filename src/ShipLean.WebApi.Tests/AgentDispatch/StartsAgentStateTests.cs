using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// The "Starts an agent" flag on a state, saved through the project settings like every other state edit.
[TestFixture]
public class StartsAgentStateTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<ArrangedProject> ArrangeAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin));
    }

    private async Task<ProjectSettingsDto> LoadAsync(ArrangedProject project)
    {
        using var response = await Api.GetAsync($"api/projects/{project.Id}/settings");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<ProjectSettingsDto>(text, Wire)!;
    }

    private async Task<ProjectSettingsDto> SaveAsync(ArrangedProject project, StateChanges states)
    {
        var loaded = await LoadAsync(project);
        var request = new ProjectSettingsRequest(
            new ProjectGeneralRequest(loaded.General.Key, loaded.General.Name, loaded.General.Description, loaded.General.WorkingDays),
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            states);
        using var response = await Api.PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, Wire));
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<ProjectSettingsDto>(text, Wire)!;
    }

    [Test]
    public async Task Settings_WhenNothingFlagged_ShouldReadEveryStateAsNotStartingAnAgent()
    {
        var project = await ArrangeAsync();

        var loaded = await LoadAsync(project);

        loaded.States.ShouldAllBe(s => !s.StartsAgent);
    }

    [Test]
    public async Task Settings_WhenAStateIsFlagged_ShouldSaveItAndKeepItThroughARename()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");

        var flagged = await SaveAsync(project, new StateChanges([], [new StateUpdate(active, "Active", StateCategory.InProgress, true)], [], []));
        var renamed = await SaveAsync(project, new StateChanges([], [new StateUpdate(active, "Doing", StateCategory.InProgress)], [], []));

        flagged.States.Single(s => s.Id == active).StartsAgent.ShouldBeTrue();
        flagged.States.Count(s => s.StartsAgent).ShouldBe(1);
        renamed.States.Single(s => s.Id == active).ShouldSatisfyAllConditions(
            s => s.Name.ShouldBe("Doing"),
            s => s.StartsAgent.ShouldBeTrue());
    }

    [Test]
    public async Task Settings_WhenAFlaggedStateIsUnflagged_ShouldSaveIt()
    {
        var project = await ArrangeAsync();
        var active = project.State(WorkItemTypeId.Story, "Active");
        await SaveAsync(project, new StateChanges([], [new StateUpdate(active, "Active", StateCategory.InProgress, true)], [], []));

        var saved = await SaveAsync(project, new StateChanges([], [new StateUpdate(active, "Active", StateCategory.InProgress, false)], [], []));

        saved.States.ShouldAllBe(s => !s.StartsAgent);
    }

    [Test]
    public async Task Settings_WhenAFlaggedStateIsAdded_ShouldSaveItFlagged()
    {
        var project = await ArrangeAsync();
        var ready = Guid.NewGuid();

        var saved = await SaveAsync(project, new StateChanges([new StateAdd(ready, WorkItemTypeId.Story, "Ready for pickup", StateCategory.Proposed, true)], [], [], []));

        saved.States.Single(s => s.Id == ready).StartsAgent.ShouldBeTrue();
    }
}
