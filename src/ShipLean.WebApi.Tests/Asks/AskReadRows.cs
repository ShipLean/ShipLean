using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Asks;

// The item asks read: any member reads, so the minimum is Member. The URL names the seeded asks' Story.
public sealed class AskReadRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/work-items/{id:guid}/asks", ProjectRole.Member, Url: $"api/work-items/{SeedIds.Asks.Item}/asks"),
    ];
}
