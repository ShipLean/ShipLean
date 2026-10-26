using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Backlog.Levels;

// The Features and Epics backlog read needs a member.
public sealed class BacklogLevelsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/backlogs/{level}", ProjectRole.Member, Url: $"api/projects/{SeedIds.Projects.Ship}/backlogs/features"),
    ];
}
