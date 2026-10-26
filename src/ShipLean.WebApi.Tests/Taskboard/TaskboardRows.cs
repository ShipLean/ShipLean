using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Taskboard;

// The Taskboard routes. The move row sends a task on Sprint 24's board with a stale revision, so it answers 409 without writing: the sweep only proves access, and the moves are the Taskboard API tests.
public sealed class TaskboardRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/sprints/{id:guid}/taskboard", ProjectRole.Member, Url: $"api/sprints/{SeedIds.Sprints.Sprint20}/taskboard"),
        new("POST", "api/sprints/{id:guid}/taskboard/moves", ProjectRole.Member, Body: $$"""{"taskId":"{{SeedIds.WorkItems.Ship171}}","expectedRevision":0,"stateId":"{{SeedIds.States.Ship.TaskNew}}","row":null,"beforeItemId":null}""", Url: $"api/sprints/{SeedIds.Sprints.Sprint24}/taskboard/moves"),
    ];
}
