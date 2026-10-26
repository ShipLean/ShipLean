using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// A work item that changes between the rollover's read and its write: nothing moves and the sprint stays pending.
[TestFixture]
public class SprintRolloverStaleApiTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IWorkItemStore>();
        services.AddSingleton<IWorkItemStore>(sp => new BumpingWorkItemStore(RealWorkItemStore.Build(sp)));
    };

    [Test]
    public async Task Rollover_WhenItemStale_ShouldBeConflictNamingTheKeyAndMoveNothing()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, first, second) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Stale sprint");
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @first", new { first });

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(first, null), new RolloverMove(second, null)]));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Error.ShouldContain(key!);
        (await connection.ExecuteScalarAsync<string>("SELECT rollover_status FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe("pending");
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE sprint_id = @sprintId", new { sprintId })).ShouldBe(2);
    }
}
