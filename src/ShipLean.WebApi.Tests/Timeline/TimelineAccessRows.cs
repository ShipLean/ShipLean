using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Timeline;

// The Feature timeline read. SHIP-103 is a seeded Feature. A member of SHIP gets in; anyone else gets not_found.
public sealed class TimelineAccessRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{projectId:guid}/work-items/by-key/{itemKey}/timeline", ProjectRole.Member,
            Url: $"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-103/timeline"),
    ];
}
