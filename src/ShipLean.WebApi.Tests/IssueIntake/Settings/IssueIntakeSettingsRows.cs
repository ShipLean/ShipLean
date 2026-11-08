using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.IssueIntake.Settings;

// The issue intake settings: reading needs Member, saving Admin. The PUT body is empty on purpose: the sweep proves
// access, and a refused body would only ever be answered after it.
public sealed class IssueIntakeSettingsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/settings/issue-intake", ProjectRole.Member),
        new("PUT", "api/projects/{id:guid}/settings/issue-intake", ProjectRole.Admin, Body: "{}"),
    ];
}
