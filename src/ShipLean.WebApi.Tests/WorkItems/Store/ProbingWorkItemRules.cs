using System.Data.Common;
using Npgsql;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// Rules double that, from a second connection while the write's transaction is open, tries to lock the project's board rows
// FOR UPDATE NOWAIT and records whether that was refused (the write holds a shared lock on the board).
public sealed class ProbingWorkItemRules(string connectionString) : IWorkItemRules
{
    public bool? BoardWasLocked { get; private set; }

    public async Task<RuleOutcome> ApplyAsync(WorkItemSnapshot? before, ProposedChange change, Guid? actorId, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var probe = new NpgsqlCommand("SELECT id FROM board WHERE project_id = @projectId FOR UPDATE NOWAIT", connection);
        probe.Parameters.AddWithValue("projectId", change.ProjectId);
        try
        {
            await probe.ExecuteNonQueryAsync(cancellationToken);
            BoardWasLocked = false;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            BoardWasLocked = true;
        }

        return new RuleOutcome(change, []);
    }
}
