using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.WorkItems.Api;

// The New Feature dialog's slug check.
public sealed class FeatureSlugRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/features/slug-check", ProjectRole.Member, Url: $"api/projects/{SeedIds.Projects.Ship}/features/slug-check?value=free-slug"),
    ];
}
