using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

// The queries the store and the read model share.
internal static class WorkItemQueries
{
    // Oldest first.
    public static async Task<IReadOnlyList<RevisionSnapshot>> RevisionRowsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid id, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<RevisionRow>(new CommandDefinition(
            """
            SELECT rev, changed_by AS changedBy, changed_at AS changedAt, original_system AS originalSystem, import_job_id AS importJobId,
                   agent_name AS agentName, webhook, changes::text AS changes
            FROM revision WHERE work_item_id = @id ORDER BY rev
            """,
            new { id }, transaction, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new RevisionSnapshot(
            r.Rev, r.ChangedBy, r.ChangedAt, r.OriginalSystem, r.ImportJobId, JsonSerializer.Deserialize<List<FieldChange>>(r.Changes, PersistedJson.Options)!, r.AgentName, r.Webhook))];
    }

    public static async Task<IReadOnlyList<WorkItemRow>> RowsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, object parameters, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<WorkItemRow>(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken))];

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<LinkSnapshot>>> LinksAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid[] ids, CancellationToken cancellationToken)
    {
        var rows = await LinkRowsAsync(connection, transaction, ids, cancellationToken);
        return rows.GroupBy(r => r.SourceId).ToDictionary(g => g.Key, g => (IReadOnlyList<LinkSnapshot>)[.. g.Select(r => r.ToSnapshot())]);
    }

    public static async Task<IReadOnlyList<WorkItemLinkRow>> LinkRowsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid[] ids, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<WorkItemLinkRow>(new CommandDefinition(WorkItemLinkRow.Select, new { ids }, transaction, cancellationToken: cancellationToken))];

    private sealed record RevisionRow(int Rev, Guid? ChangedBy, DateTimeOffset ChangedAt, string? OriginalSystem, Guid? ImportJobId, string? AgentName, string? Webhook, string Changes);
}
