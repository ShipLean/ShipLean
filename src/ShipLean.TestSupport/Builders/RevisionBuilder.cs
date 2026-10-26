using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Builders;

// Arranges one revision row for a work item. changes is a raw JSON array string (the persisted FieldChange[] shape);
// this builder knows no persisted types. The default is revision 2 with an empty change list, changed now by no one.
public sealed class RevisionBuilder(Guid workItemId)
{
    private int _rev = 2;
    private Guid? _changedBy;
    private DateTimeOffset? _changedAt;
    private string _changes = "[]";
    private string? _originalSystem;
    private Guid? _importJobId;

    public RevisionBuilder WithRev(int rev)
    {
        _rev = rev;
        return this;
    }

    public RevisionBuilder WithChangedBy(Guid? userId)
    {
        _changedBy = userId;
        return this;
    }

    public RevisionBuilder WithChangedAt(DateTimeOffset changedAt)
    {
        _changedAt = changedAt;
        return this;
    }

    public RevisionBuilder WithChanges(string changesJson)
    {
        _changes = changesJson;
        return this;
    }

    public RevisionBuilder WithImport(string originalSystem, Guid importJobId)
    {
        _originalSystem = originalSystem;
        _importJobId = importJobId;
        return this;
    }

    // Inserts the revision and returns its revision number. Does not touch work_item.revision.
    public async Task<int> InsertAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO revision (work_item_id, rev, changed_by, changed_at, original_system, import_job_id, changes)
            VALUES (@workItemId, @rev, @changedBy::uuid, coalesce(@changedAt::timestamptz, now()), @originalSystem::text, @importJobId::uuid, @changes::jsonb)
            """,
            new
            {
                workItemId,
                rev = _rev,
                changedBy = _changedBy,
                changedAt = _changedAt?.ToUniversalTime(),
                originalSystem = _originalSystem,
                importJobId = _importJobId,
                changes = _changes,
            });
        return _rev;
    }
}
