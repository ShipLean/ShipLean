using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Backups.Http;

// The backup routes: instance admin only.
// The allowed leg's host answers them through FakeBackupAdmin (AllowedSweepTests), because the real admin answers
// 503 not_configured until backups are set up.
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
