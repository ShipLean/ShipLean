using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// The integrations routes: instance admin only.
public sealed class IntegrationsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/instance/integrations", ProjectRole.Member, InstanceScoped: true),
        new("PUT", "api/instance/integrations", ProjectRole.Member, Body: """{"clearGitHubToken":false,"clearGitLabToken":false}""", InstanceScoped: true),
    ];
}
