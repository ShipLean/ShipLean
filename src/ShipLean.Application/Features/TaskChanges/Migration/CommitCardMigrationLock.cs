namespace ShipLean.Application.Features.TaskChanges.Migration;

// The migration's session advisory lock: pg_try_advisory_lock(NAMESPACE, KEY), the two-int form, so one instance runs the job.
// Public so a test can hold it from another connection.
public static class CommitCardMigrationLock
{
    public const int NAMESPACE = 0x43_43_4D_47;
    public const int KEY = 1;

    public const string TrySql = "SELECT pg_try_advisory_lock(@namespace, @key)";
    public const string UnlockSql = "SELECT pg_advisory_unlock(@namespace, @key)";

    public static readonly object Parameters = new { @namespace = NAMESPACE, key = KEY };
}
