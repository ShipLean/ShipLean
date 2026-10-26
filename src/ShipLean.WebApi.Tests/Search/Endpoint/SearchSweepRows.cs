using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Search.Endpoint;

// The search route. A member of SHIP gets in; anyone else asking for project SHIP gets not_found.
public sealed class SearchSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/search", ProjectRole.Member, Url: "api/search?q=anything&project=SHIP"),
    ];
}
