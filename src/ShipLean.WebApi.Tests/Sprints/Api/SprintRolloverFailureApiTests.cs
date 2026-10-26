using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// A work item store that throws (pinned through ConfigureTestServices): the sprint must stay pending and no item may move.
[TestFixture]
public class SprintRolloverFailureApiTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IWorkItemStore>();
        services.AddSingleton<IWorkItemStore, ThrowingWorkItemStore>();
    };

    [Test]
    public async Task Rollover_WhenStoreThrows_ShouldLeaveStatusPending()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, first, second) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Failing sprint");

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(first, null), new RolloverMove(second, null)]));

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await connection.ExecuteScalarAsync<string>("SELECT rollover_status FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe("pending");
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE sprint_id = @sprintId", new { sprintId })).ShouldBe(2);
    }
}
