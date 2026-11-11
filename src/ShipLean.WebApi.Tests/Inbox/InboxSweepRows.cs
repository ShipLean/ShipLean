using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Inbox;

// The Inbox reads: any member reads their own Inbox and Answered list, so the minimum is Member. The mention routes are
// allow-listed (RouteAllowList): the mention names its own user and project.
public sealed class InboxSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{projectId:guid}/inbox", ProjectRole.Member),
        new("GET", "api/projects/{projectId:guid}/inbox/answered", ProjectRole.Member),
    ];
}
