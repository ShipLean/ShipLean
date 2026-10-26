using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// The rollover over the real work item store (pinned through ConfigureTestServices: the app's own registration may still be the fake).
[TestFixture]
public class SprintRolloverApiTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IWorkItemStore>();
        services.AddSingleton<IWorkItemStore>(sp => new WorkItemStore(
            sp.GetRequiredService<NpgsqlDataSource>(), sp.GetRequiredService<ICurrentUser>(), new PassThroughWorkItemRules(),
            sp.GetRequiredService<IProjectEvents>(), new MarkdownRenderer(), TimeProvider.System, AgentIdentity.None));
    };

    [Test]
    public async Task Rollover_WhenApplied_ShouldMoveItemsAndMarkDecided()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, toSprint, toBacklog) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Rolled sprint");
        var before = await Api.GetAsync<RolloverDto>($"api/sprints/{sprintId}/rollover");
        before.Status.ShouldBe(RolloverStatus.Pending);
        before.Items.Select(i => i.WorkItemId).ShouldBe([toSprint, toBacklog], ignoreOrder: true);
        before.NextSprint!.Id.ShouldBe(SeedIds.Sprints.Sprint19);
        before.Items.ShouldAllBe(i => i.DefaultTargetSprintId == SeedIds.Sprints.Sprint19);
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);

        using var response = await Api.PostJsonAsync(
            $"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(toSprint, SeedIds.Sprints.Sprint24), new RolloverMove(toBacklog, null)]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.ReadAsync<SprintDto>()).RolloverStatus.ShouldBe(RolloverStatus.Decided);
        (await connection.ExecuteScalarAsync<string>("SELECT rollover_status FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe("decided");
        (await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @toSprint", new { toSprint })).ShouldBe(SeedIds.Sprints.Sprint24);
        (await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @toBacklog", new { toBacklog })).ShouldBeNull();
        (await connection.ExecuteScalarAsync<int>("SELECT revision FROM work_item WHERE id = @toSprint", new { toSprint })).ShouldBe(2);
        (await watch.NextAsync<SprintChanged>(e => e.SprintId == sprintId)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
        (await Api.GetAsync<RolloverDto>($"api/sprints/{sprintId}/rollover")).Items.ShouldBeEmpty();
    }

    [Test]
    public async Task Rollover_WhenItemIsNotInTheSprint_ShouldBeValidationAndWriteNothing()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, inSprint, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Strict sprint");
        var (_, elsewhere, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Other sprint");

        using var response = await Api.PostJsonAsync(
            $"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(inSprint, null), new RolloverMove(elsewhere, null)]));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await connection.ExecuteScalarAsync<string>("SELECT rollover_status FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe("pending");
        (await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @inSprint", new { inSprint })).ShouldBe(sprintId);
    }
}
