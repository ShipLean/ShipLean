using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// The Stories backlog read needs a member.
public sealed class BacklogStoriesRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/backlog", ProjectRole.Member),
    ];
}
