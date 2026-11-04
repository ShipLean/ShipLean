using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Demos;

// The sprint demo, demo settings and demo brand routes at their least role. The anonymous upload and logo-download routes are on RouteAllowList.
public sealed class DemoAccessSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/sprints/{id:guid}/demo", ProjectRole.Member, Url: $"api/sprints/{SeedIds.Sprints.Sprint22}/demo"),
        new("POST", "api/sprints/{id:guid}/demo", ProjectRole.Member, Url: $"api/sprints/{SeedIds.Sprints.Sprint22}/demo"),
        new("POST", "api/sprints/{id:guid}/demo/youtube/retry", ProjectRole.Member, Url: $"api/sprints/{SeedIds.Sprints.Sprint22}/demo/youtube/retry"),
        new("GET", "api/projects/{id:guid}/demo-settings", ProjectRole.Member),
        new("PUT", "api/projects/{id:guid}/demo-settings", ProjectRole.Admin, Body: """{"automatic":false,"environment":null}"""),
        new("GET", "api/projects/{id:guid}/demo-brand/logo", ProjectRole.Member),
        new("PUT", "api/projects/{id:guid}/demo-brand/logo", ProjectRole.Admin),
        new("DELETE", "api/projects/{id:guid}/demo-brand/logo", ProjectRole.Admin),
        new("PUT", "api/projects/{id:guid}/demo-brand/tokens", ProjectRole.Admin, Body: """{"tokens":null}"""),
    ];
}
