using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.ProjectSettings;

// The settings page: reading needs Member, saving Admin. The PUT body is empty on purpose: the sweep proves access, and a
// refused body would only ever be answered after it. Leave is the one destructive row; it is a member-level route, and
// the sweep's allowed leg runs in a fixture of its own.
public sealed class ProjectSettingsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/settings", ProjectRole.Member),
        new("PUT", "api/projects/{id:guid}/settings", ProjectRole.Admin, Body: "{}"),
        new("POST", "api/projects/{id:guid}/leave", ProjectRole.Member),
    ];
}
