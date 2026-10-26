using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Instance;

// The instance routes: instance admin only. The join row points at OPS, which no other row or instance test joins.
public sealed class InstanceRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/instance/settings", ProjectRole.Member, InstanceScoped: true),
        new("PUT", "api/instance/settings", ProjectRole.Member, Body: """{"name":"ShipLean","baseUrl":"http://localhost:5000","projectCreation":"everyone"}""", InstanceScoped: true),
        new("GET", "api/instance/projects", ProjectRole.Member, InstanceScoped: true),
        new("POST", "api/instance/projects/{id:guid}/join", ProjectRole.Member, Url: $"api/instance/projects/{SeedIds.Projects.Ops}/join", InstanceScoped: true),
    ];
}
