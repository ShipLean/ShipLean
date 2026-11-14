using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Timeline;

// The Feature timeline read and its two usage reports. SHIP-103 is a seeded Feature. A member of SHIP gets in; anyone else gets
// not_found.
public sealed class TimelineAccessRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{projectId:guid}/work-items/by-key/{itemKey}/timeline", ProjectRole.Member,
            Url: $"api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-103/timeline"),
        new("POST", "api/projects/{id:guid}/usage/timeline-opened", ProjectRole.Member,
            Body: """{"from":"feature"}""", Url: $"api/projects/{SeedIds.Projects.Ship}/usage/timeline-opened"),
        new("POST", "api/projects/{id:guid}/usage/timeline-bar-selected", ProjectRole.Member,
            Body: "{}", Url: $"api/projects/{SeedIds.Projects.Ship}/usage/timeline-bar-selected"),
    ];
}
