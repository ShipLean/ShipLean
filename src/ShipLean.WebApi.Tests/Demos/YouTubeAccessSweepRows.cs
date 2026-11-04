using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Demos;

// The project-scoped YouTube routes at their least role. The callback is anonymous and is on RouteAllowList.
public sealed class YouTubeAccessSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/projects/{id:guid}/youtube/start", ProjectRole.Admin),
        new("POST", "api/projects/{id:guid}/youtube/check", ProjectRole.Admin, Body: """{"visibility":"unlisted"}"""),
        new("POST", "api/projects/{id:guid}/youtube/disconnect", ProjectRole.Admin),
    ];
}
