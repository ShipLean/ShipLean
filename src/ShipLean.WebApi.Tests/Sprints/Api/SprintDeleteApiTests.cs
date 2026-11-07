using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// DELETE api/sprints/{id}: archives a sprint nothing is planned into. Each test works on sprints of its own far in the future.
[TestFixture]
public class SprintDeleteApiTests : ApiFixture
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private async Task<SprintDto> CreateAsync(string name, int startOffset)
    {
        using var response = await Api.PostJsonAsync($"api/projects/{SeedIds.Projects.Ship}/sprints", new SprintCreate(name, Today.AddDays(startOffset), Today.AddDays(startOffset + 13), ""));
        return await response.ReadAsync<SprintDto>();
    }

    private async Task<List<SprintDto>> ListAsync() => await Api.GetAsync<List<SprintDto>>($"api/projects/{SeedIds.Projects.Ship}/sprints");

    [Test]
    public async Task Delete_WhenSprintHasNoWorkItems_ShouldArchiveItPublishAndTrack()
    {
        var sprint = await CreateAsync("Empty sprint", 2000);
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);

        using var response = await Api.DeleteAsync($"api/sprints/{sprint.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await ListAsync()).ShouldNotContain(s => s.Id == sprint.Id);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<bool>("SELECT archived_at IS NOT NULL FROM sprint WHERE id = @id", new { id = sprint.Id })).ShouldBeTrue();
        (await watch.NextAsync<SprintChanged>(e => e.SprintId == sprint.Id)).ClientId.ShouldBe(ApiClient.ClientId);
        var tracked = await Recorder.WaitForAsync(e => e.Source == "api:Sprints.Delete", TimeSpan.FromSeconds(10));
        tracked.Name.ShouldBe(UsageEventNames.SprintDeleted);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Delete_WhenDeleted_ShouldFreeItsNameAndDatesAndRefuseASecondDelete()
    {
        var sprint = await CreateAsync("Reused sprint", 2100);
        using var deleted = await Api.DeleteAsync($"api/sprints/{sprint.Id}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var again = await CreateAsync("Reused sprint", 2100);
        using var second = await Api.DeleteAsync($"api/sprints/{sprint.Id}");

        again.Id.ShouldNotBe(sprint.Id);
        (await ApiClient.ReadApiErrorAsync(second, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Delete_WhenSprintHasWorkItems_ShouldBeRefusedAndKeepIt()
    {
        var sprint = await CreateAsync("Planned sprint", 2200);
        await using var connection = await OpenDatabaseAsync();
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Planned story").WithSprint(sprint.Id).InsertAsync(connection);

        using var response = await Api.DeleteAsync($"api/sprints/{sprint.Id}");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Error.ShouldBe("Planned sprint has work items, so it can't be deleted. Move them to another sprint or the backlog first.");
        (await ListAsync()).ShouldContain(s => s.Id == sprint.Id);
    }

    [Test]
    public async Task Delete_WhenItsOnlyWorkItemIsRemoved_ShouldStillBeRefused()
    {
        var sprint = await CreateAsync("Removed-only sprint", 2300);
        await using var connection = await OpenDatabaseAsync();
        var removed = await connection.ExecuteScalarAsync<Guid>(
            "SELECT id FROM state WHERE project_id = @project AND type = 'story' AND category = 'removed' LIMIT 1", new { project = SeedIds.Projects.Ship });
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Removed story").WithState(removed).WithSprint(sprint.Id).InsertAsync(connection);

        using var response = await Api.DeleteAsync($"api/sprints/{sprint.Id}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Delete_WhenSprintHasARetrospective_ShouldBeRefused()
    {
        var sprint = await CreateAsync("Retro sprint", 2400);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO retrospective (id, project_id, sprint_id, author_user_id, role) VALUES (@id, @project, @sprint, @joel, 'person')",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, sprint = sprint.Id, joel = SeedIds.Users.Joel });

        using var response = await Api.DeleteAsync($"api/sprints/{sprint.Id}");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict);
        error.Error.ShouldBe("Retro sprint has a retrospective, so it can't be deleted.");
    }

    // The race the row locks close: a work item write that checked the sprint before a delete committed must not land the
    // item in the archived sprint. The delete is held open by hand (its lock and its archive), the write is sent, and once it
    // waits on the sprint row the delete commits.
    [Test]
    public async Task PatchWorkItem_WhenSprintIsDeletedWhileTheWriteWaits_ShouldBeNotFound()
    {
        var sprint = await CreateAsync("Raced sprint", 2500);
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Raced story").InsertAsync(connection);
        await using var deleter = await OpenDatabaseAsync();
        await using var delete = await deleter.BeginTransactionAsync();
        await deleter.ExecuteAsync("SELECT 1 FROM sprint WHERE id = @id FOR UPDATE", new { id = sprint.Id }, delete);
        await deleter.ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE id = @id", new { id = sprint.Id }, delete);

        var patch = Api.PatchJsonAsync($"api/work-items/{item}", new { expectedRevision = 1, changes = new { sprint = new { value = sprint.Id } } });
        await WaitForALockWaiterAsync(connection);
        await delete.CommitAsync();
        using var response = await patch;

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Error.ShouldBe("Sprint not found.");
        (await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @item", new { item })).ShouldBeNull();
    }

    // Polls the server (each poll is a round trip, no sleep) until another backend waits on a lock.
    private static async Task WaitForALockWaiterAsync(NpgsqlConnection watcher)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!timeout.IsCancellationRequested)
        {
            if (await watcher.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND pid <> pg_backend_pid())"))
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException("The work item write never waited on the sprint row.");
    }
}
