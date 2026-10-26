using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Search.Doubles;

// The search admin route: instance admin only.
public sealed class SearchAdminSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/admin/search/reindex", ProjectRole.Member, InstanceScoped: true),
    ];
}
