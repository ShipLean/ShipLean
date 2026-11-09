namespace ShipLean.Application.Features.Backups.Runs;

// The text the backup_run trigger and state columns hold.
internal static class BackupRunWire
{
    public static string Trigger(BackupTriggerKind trigger) => trigger switch
    {
        BackupTriggerKind.Scheduled => "scheduled",
        BackupTriggerKind.Manual => "manual",
        _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
    };

    public static BackupTriggerKind Trigger(string trigger) => trigger switch
    {
        "scheduled" => BackupTriggerKind.Scheduled,
        "manual" => BackupTriggerKind.Manual,
        _ => throw new InvalidOperationException($"Unknown backup trigger '{trigger}'."),
    };

    public static BackupRunState State(string state) => state switch
    {
        "running" => BackupRunState.Running,
        "succeeded" => BackupRunState.Succeeded,
        "failed" => BackupRunState.Failed,
        "skipped" => BackupRunState.Skipped,
        _ => throw new InvalidOperationException($"Unknown backup run state '{state}'."),
    };
}
