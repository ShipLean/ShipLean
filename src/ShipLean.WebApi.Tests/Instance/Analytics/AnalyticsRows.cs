using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Instance.Analytics;

// The analytics routes: instance admin only.
public sealed class AnalyticsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/instance/analytics", ProjectRole.Member, InstanceScoped: true),
        new("PUT", "api/instance/analytics", ProjectRole.Member, Body: """{"provider":"none","host":null,"siteKey":null,"sendNames":false}""", InstanceScoped: true),
        new("POST", "api/instance/analytics/test", ProjectRole.Member, InstanceScoped: true),
    ];
}
