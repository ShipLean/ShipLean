using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Sprints.Metrics;

// The metric reads. They need the same role as the other sprint reads (a member); the sprint route points at a SHIP sprint.
public sealed class MetricsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/sprints/{id:guid}/metrics", ProjectRole.Member, Url: $"api/sprints/{SeedIds.Sprints.Sprint22}/metrics"),
        new("GET", "api/projects/{id:guid}/velocity", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/forecast", ProjectRole.Member),
    ];
}
