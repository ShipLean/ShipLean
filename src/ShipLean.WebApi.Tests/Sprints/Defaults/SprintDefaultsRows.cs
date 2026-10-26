using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Sprints.Defaults;

// The New sprint defaults read.
public sealed class SprintDefaultsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/sprints/next", ProjectRole.Member),
    ];
}
