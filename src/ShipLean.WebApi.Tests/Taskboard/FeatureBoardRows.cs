using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Taskboard;

// The Feature board read. SHIP-103 is a seeded Feature.
public sealed class FeatureBoardRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{projectId:guid}/work-items/by-key/{itemKey}/feature-board", ProjectRole.Member, Url: $"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-103/feature-board"),
    ];
}
