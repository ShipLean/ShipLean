using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// The rollover's own rules over the real work item store: a story takes its open tasks of the ended sprint with it,
// a sprint already decided is refused, and items sent back land on top of the backlog.
[TestFixture]
public class SprintRolloverTasksApiTests : ApiFixture
{
    private Func<Task>? _interfere;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IWorkItemStore>();
        services.AddSingleton<IWorkItemStore>(sp => new InterferingWorkItemStore(RealWorkItemStore.Build(sp), TakeInterferenceAsync));
    };

    // The change runs once, before the first batch patch of the test that set it.
    private async Task TakeInterferenceAsync()
    {
        if (Interlocked.Exchange(ref _interfere, null) is { } run)
        {
            await run();
        }
    }

    [Test]
    public async Task Rollover_WhenStoryMoves_ShouldMoveItsOpenTasksOfThisSprintAndLeaveTheRest()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, story, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Tasks sprint");
        var open = await InsertTaskAsync(connection, story, sprintId, closed: false);
        var closed = await InsertTaskAsync(connection, story, sprintId, closed: true);
        var elsewhere = await InsertTaskAsync(connection, story, SeedIds.Sprints.Sprint19, closed: false);

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(story, SeedIds.Sprints.Sprint24)]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await SprintOfAsync(connection, open)).ShouldBe(SeedIds.Sprints.Sprint24);
        (await SprintOfAsync(connection, closed)).ShouldBe(sprintId);
        (await SprintOfAsync(connection, elsewhere)).ShouldBe(SeedIds.Sprints.Sprint19);
    }

    [Test]
    public async Task Rollover_WhenStoryGoesToBacklog_ShouldTakeItsOpenTasksOffTheSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, story, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Backlog tasks sprint");
        var open = await InsertTaskAsync(connection, story, sprintId, closed: false);

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(story, null)]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await SprintOfAsync(connection, open)).ShouldBeNull();
    }

    [Test]
    public async Task Rollover_WhenTaskIsListedAsWell_ShouldMoveItOnce()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, story, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Listed task sprint");
        var open = await InsertTaskAsync(connection, story, sprintId, closed: false);

        using var response = await Api.PostJsonAsync(
            $"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(story, SeedIds.Sprints.Sprint24), new RolloverMove(open, SeedIds.Sprints.Sprint24)]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await SprintOfAsync(connection, open)).ShouldBe(SeedIds.Sprints.Sprint24);
    }

    [Test]
    public async Task Rollover_WhenTaskIsClosedBetweenReadAndApply_ShouldLeaveItInTheSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, story, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Closed meanwhile sprint");
        var task = await InsertTaskAsync(connection, story, sprintId, closed: false);
        var other = await InsertTaskAsync(connection, story, sprintId, closed: false);
        _interfere = () => connection.ExecuteAsync(
            "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @projectId AND type = 'task' AND category = 'completed' LIMIT 1), revision = revision + 1 WHERE id = @task",
            new { projectId = SeedIds.Projects.Ship, task });

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(story, SeedIds.Sprints.Sprint24)]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await SprintOfAsync(connection, task)).ShouldBe(sprintId);
        (await SprintOfAsync(connection, other)).ShouldBe(SeedIds.Sprints.Sprint24);
    }

    [Test]
    public async Task Rollover_WhenTaskIsAddedBetweenReadAndApply_ShouldMoveItWithItsStory()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, story, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Added meanwhile sprint");
        Guid? added = null;
        _interfere = async () => added = await InsertTaskAsync(connection, story, sprintId, closed: false);

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(story, SeedIds.Sprints.Sprint24)]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        added.ShouldNotBeNull();
        (await SprintOfAsync(connection, added.Value)).ShouldBe(SeedIds.Sprints.Sprint24);
    }

    [Test]
    public async Task Rollover_WhenSprintIsAlreadyDecided_ShouldBeConflictAndMoveNothing()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, story, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Decided sprint");
        await connection.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = @sprintId", new { sprintId });

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(story, null)]));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Error.ShouldContain("already been dealt with");
        (await SprintOfAsync(connection, story)).ShouldBe(sprintId);
    }

    [Test]
    public async Task Rollover_WhenRead_ShouldCountOnlyTheTasksInThisSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, story, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Counted sprint");
        await InsertTaskAsync(connection, story, sprintId, closed: false);
        await InsertTaskAsync(connection, story, sprintId, closed: true);
        await InsertTaskAsync(connection, story, SeedIds.Sprints.Sprint19, closed: false);

        var rollover = await Api.GetAsync<RolloverDto>($"api/sprints/{sprintId}/rollover");

        var item = rollover.Items.Single(i => i.WorkItemId == story);
        (item.OpenTasks, item.TotalTasks).ShouldBe((1, 2));
    }

    [Test]
    public async Task Rollover_WhenReadForAnEndedSprintWithNothingUnfinished_ShouldMarkItDecidedAndLeaveAnOlderDeferredOne()
    {
        await using var connection = await OpenDatabaseAsync();
        var (deferred, _, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Deferred sprint");
        var empty = await SprintRolloverSupport.ArrangeEmptyEndedSprintAsync(connection, "Empty sprint");

        var read = await Api.GetAsync<RolloverDto>($"api/sprints/{empty}/rollover");

        read.Status.ShouldBe(RolloverStatus.Decided);
        (await connection.ExecuteScalarAsync<string>("SELECT rollover_status FROM sprint WHERE id = @empty", new { empty })).ShouldBe("decided");
        (await connection.ExecuteScalarAsync<string>("SELECT rollover_status FROM sprint WHERE id = @deferred", new { deferred })).ShouldBe("pending");
    }

    [Test]
    public async Task Rollover_WhenItemsGoToBacklog_ShouldPutThemOnTopInTheOrderSent()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, first, second) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Top of backlog sprint");
        var topBefore = await connection.ExecuteScalarAsync<string>(
            "SELECT min(backlog_order COLLATE \"C\") FROM work_item WHERE project_id = @projectId AND id NOT IN (@first, @second)", new { projectId = SeedIds.Projects.Ship, first, second });

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(second, null), new RolloverMove(first, null)]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var orders = (await connection.QueryAsync<(Guid Id, string Order)>(
            "SELECT id, backlog_order AS \"Order\" FROM work_item WHERE project_id = @projectId ORDER BY backlog_order COLLATE \"C\" LIMIT 3", new { projectId = SeedIds.Projects.Ship })).ToList();
        orders.Take(2).Select(o => o.Id).ShouldBe([second, first]);
        string.CompareOrdinal(orders[1].Order, topBefore).ShouldBeLessThan(0);
    }

    private static async Task<Guid> InsertTaskAsync(NpgsqlConnection connection, Guid parent, Guid sprintId, bool closed)
    {
        var builder = new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("A task").WithParent(parent).WithSprint(sprintId);
        if (closed)
        {
            builder.WithState(await connection.ExecuteScalarAsync<Guid>(
                "SELECT id FROM state WHERE project_id = @projectId AND type = 'task' AND category = 'completed' LIMIT 1", new { projectId = SeedIds.Projects.Ship }));
        }

        return await builder.InsertAsync(connection);
    }

    private static Task<Guid?> SprintOfAsync(NpgsqlConnection connection, Guid id)
        => connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @id", new { id });
}
