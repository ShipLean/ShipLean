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

// The sprint is marked decided inside the work item store's transaction: when that transaction fails after the hook, the mark is undone too.
[TestFixture]
public class SprintRolloverAtomicApiTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IWorkItemStore>();
        services.AddSingleton<IWorkItemStore>(sp => new HookObservingWorkItemStore(RealWorkItemStore.Build(sp)));
    };

    [Test]
    public async Task Rollover_WhenApplied_ShouldMarkDecidedInsideTheWrite()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, first, second) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Observed sprint");
        HookObservingWorkItemStore.SeenInside.Clear();

        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(first, null), new RolloverMove(second, null)]));

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        HookObservingWorkItemStore.SeenInside.ShouldBe(["decided"]);
        (await connection.ExecuteScalarAsync<string>("SELECT rollover_status FROM sprint WHERE id = @sprintId", new { sprintId })).ShouldBe("pending");
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE sprint_id = @sprintId", new { sprintId })).ShouldBe(2);
    }
}
