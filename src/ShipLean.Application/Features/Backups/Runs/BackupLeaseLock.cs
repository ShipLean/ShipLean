namespace ShipLean.Application.Features.Backups.Runs;

// The lease's session advisory lock: pg_try_advisory_lock(NAMESPACE, KEY), the two-int form, kept apart from the one-bigint
// form and from other namespaces. Advisory locks are per database.
internal static class BackupLeaseLock
{
    public const int NAMESPACE = 0x42_4B_55_50;
    public const int KEY = 1;

    public const string TrySql = "SELECT pg_try_advisory_lock(@namespace, @key)";
    public const string UnlockSql = "SELECT pg_advisory_unlock(@namespace, @key)";

    // True while this session holds the lock.
    public const string HeldSql =
        """
        SELECT EXISTS (
            SELECT 1 FROM pg_locks
            WHERE locktype = 'advisory' AND classid = @namespace AND objid = @key AND objsubid = 2 AND pid = pg_backend_pid() AND granted)
        """;

    public static readonly object Parameters = new { @namespace = NAMESPACE, key = KEY };
}
