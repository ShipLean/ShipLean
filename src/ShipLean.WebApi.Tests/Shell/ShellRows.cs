using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Shell;

// The project routes of the shell reads. GET api/projects filters by membership itself and preferences are the caller's own,
// so neither has a row.
public sealed class ShellRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/by-key/{key}", ProjectRole.Member, Url: "api/projects/by-key/SHIP"),
        new("GET", "api/projects/{id:guid}", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/sprints", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/members", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/states", ProjectRole.Member),
    ];
}
