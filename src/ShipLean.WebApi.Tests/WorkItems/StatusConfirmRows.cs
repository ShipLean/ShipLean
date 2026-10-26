using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.WorkItems;

// Still true. The allowed leg sends a stale revision, so it gets a 409 and writes nothing.
public sealed class StatusConfirmRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/work-items/{id:guid}/status/confirm", ProjectRole.Member, Body: """{"revision":999}""", Url: $"api/work-items/{SeedIds.WorkItems.Ship102}/status/confirm"),
    ];
}
