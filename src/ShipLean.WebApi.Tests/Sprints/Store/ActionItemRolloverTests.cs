using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Contracts.WorkItems;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Sprints.Store;

// An unfinished action item in an ended sprint is offered for rollover, and its points count in the next sprint's load.
[TestFixture]
public class ActionItemRolloverTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IWorkItemStore>();
        services.AddSingleton<IWorkItemStore>(sp => new WorkItemStore(
            sp.GetRequiredService<NpgsqlDataSource>(), sp.GetRequiredService<ICurrentUser>(), new PassThroughWorkItemRules(),
            sp.GetRequiredService<IProjectEvents>(), new MarkdownRenderer(), TimeProvider.System, AgentIdentity.None));
    };

    [Test]
    public async Task Rollover_WhenActionItemUnfinished_ShouldOfferItAndMoveItLikeAStory()
    {
        await using var connection = await OpenDatabaseAsync();
        var (sprintId, _, _) = await SprintRolloverSupport.ArrangeEndedSprintAsync(connection, "Action rollover sprint");
        var action = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Unfinished action").WithPoints(4).WithSprint(sprintId).InsertAsync(connection);

        var offered = await Api.GetAsync<RolloverDto>($"api/sprints/{sprintId}/rollover");
        offered.Items.ShouldContain(item => item.WorkItemId == action && item.Type == WorkItemTypeId.Action);
        var next = offered.NextSprint.ShouldNotBeNull();
        var plannedBefore = offered.NextSprintPoints;
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle("Planned action").WithPoints(3).WithSprint(next.Id).InsertAsync(connection);

        var planned = await Api.GetAsync<RolloverDto>($"api/sprints/{sprintId}/rollover");

        planned.NextSprintPoints.ShouldBe(plannedBefore + 3m);
        using var response = await Api.PostJsonAsync($"api/sprints/{sprintId}/rollover", new RolloverRequest([new RolloverMove(action, next.Id)]));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await Api.GetAsync<RolloverDto>($"api/sprints/{sprintId}/rollover")).Items.ShouldNotContain(item => item.WorkItemId == action);
        (await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @action", new { action })).ShouldBe(next.Id);
    }
}
