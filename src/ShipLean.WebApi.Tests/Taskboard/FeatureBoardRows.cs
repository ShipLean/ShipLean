using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Taskboard;

// The Feature board routes. SHIP-103 is a seeded Feature. The move row sends SHIP-171 (under SHIP-141 in Feature SHIP-101) with a stale revision, so it answers 409 without writing.
public sealed class FeatureBoardRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{projectId:guid}/work-items/by-key/{itemKey}/feature-board", ProjectRole.Member, Url: $"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-103/feature-board"),
        new("POST", "api/work-items/{featureId:guid}/feature-board/moves", ProjectRole.Member, Body: $$"""{"taskId":"{{SeedIds.WorkItems.Ship171}}","expectedRevision":0,"stateId":"{{SeedIds.States.Ship.TaskNew}}","row":null,"beforeItemId":null}""", Url: $"api/work-items/{SeedIds.WorkItems.Ship101}/feature-board/moves"),
    ];
}
