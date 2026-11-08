using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Usage.BacklogSort;

// The backlog sort report. A member of SHIP gets in; anyone else gets not_found.
public sealed class BacklogSortSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/projects/{id:guid}/usage/backlog-sort", ProjectRole.Member,
            Body: """{"screen":"storiesBacklog","column":"points","direction":"asc"}"""),
    ];
}
