using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Usage.BoardFilter;

// The board filter's two usage reports. A member of SHIP gets in; anyone else gets not_found.
public sealed class BoardFilterSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/projects/{id:guid}/usage/board-filter-opened", ProjectRole.Member,
            Body: """{"level":"features"}"""),
        new("POST", "api/projects/{id:guid}/usage/board-health-filtered", ProjectRole.Member,
            Body: """{"health":"onTrack"}"""),
    ];
}
