using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.ProjectSettings;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.ProjectSettings.Api;

// A work item that enters a removed state after the save read its plan. The work item store is wrapped (ConfigureTestServices)
// so a test can write between the plan and the save's transaction, which is where a concurrent write would land.
[TestFixture]
public class ProjectSettingsLateItemTests : ApiFixture
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

    [Test]
    public async Task Settings_WhenItemEntersRemovedStateAfterLoad_ShouldBeConflict()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, ProjectRole.Admin));
        var active = project.State(WorkItemTypeId.Story, "Active");
        var target = project.State(WorkItemTypeId.Story, "New");
        var loaded = await Api.GetAsync<ProjectSettingsDto>($"api/projects/{project.Id}/settings");
        var request = new ProjectSettingsRequest(
            new ProjectGeneralRequest(loaded.General.Key, "Should not be saved", loaded.General.Description, loaded.General.WorkingDays),
            [.. loaded.Members.Select(m => new MemberPut(m.Email, m.Role))],
            loaded.Sprints,
            new StateChanges([], [], [], [new StateRemove(active, target)]));
        Guid late = Guid.Empty;
        _beforePatchMany = async () =>
        {
            await using var other = await OpenDatabaseAsync();
            late = await new WorkItemBuilder(project.Id).WithState(active).InsertAsync(other);
        };

        using var response = await Api.PutJsonAsync($"api/projects/{project.Id}/settings", JsonSerializer.SerializeToElement(request, WireOptions));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        late.ShouldNotBe(Guid.Empty);
        var row = await connection.QuerySingleAsync<(Guid StateId, int Revision)>("SELECT state_id, revision FROM work_item WHERE id = @late", new { late });
        row.StateId.ShouldBe(active);
        row.Revision.ShouldBe(1);
        (await connection.ExecuteScalarAsync<bool>("SELECT archived_at IS NULL FROM state WHERE id = @active", new { active })).ShouldBeTrue();
        (await connection.ExecuteScalarAsync<string>("SELECT name FROM project WHERE id = @id", new { id = project.Id })).ShouldNotBe("Should not be saved");
    }

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
