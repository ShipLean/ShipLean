using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Backups.Http;

// The backup routes: instance admin only.
// SHIP-782: AccessSweep_WhenAllowed_ShouldPassAccess is red for PUT, test, cron-preview and run while PendingBackupAdmin
// is registered: C-HTTP has it answer 503 not_configured, and the allowed leg refuses 5xx. Run stays 503 after
// BackupAdmin lands unless the allowed leg's host has backups configured. Reported to the conductor for a decision.
public sealed class BackupsSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/instance/backups", ProjectRole.Member, InstanceScoped: true),
        new("PUT", "api/instance/backups", ProjectRole.Member, Body: BackupRoutes.SaveBody, InstanceScoped: true),
        new("POST", "api/instance/backups/test", ProjectRole.Member, Body: BackupRoutes.DestinationBody, InstanceScoped: true),
        new("POST", "api/instance/backups/cron-preview", ProjectRole.Member, Body: BackupRoutes.CronBody, InstanceScoped: true),
        new("POST", "api/instance/backups/run", ProjectRole.Member, InstanceScoped: true),
    ];
}
