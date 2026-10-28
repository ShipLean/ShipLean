using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.AgentDispatch;

// Decides whether a work item event starts a session, and starts it. The bus is in process and not durable: a session lost
// between commit and this handler is what the panel's Start agent is for.
// A created item starts one if it lands in a flagged state and was not imported. A moved or updated item starts one when the
// revision the event names changed its state (so a reorder inside the state does not), that revision is by a person and
// not an import or webhook, it is recent (a revision from long ago is not "entering" the state now), and the state it is in
// now starts an agent. The person is the session's creator. No live session, connection and repository are checked by the starter.
public sealed class AgentTrigger(NpgsqlDataSource dataSource, AgentSessionStarter starter, TimeProvider time, ILogger<AgentTrigger> logger)
{
    private static readonly TimeSpan Recent = TimeSpan.FromMinutes(5);

    public async Task HandleAsync(ProjectEvent projectEvent, CancellationToken cancellationToken = default)
    {
        var (workItemId, revision) = projectEvent switch
        {
            WorkItemCreated created => (created.Id, (int?)null),
            WorkItemMoved moved => (moved.Id, moved.Revision),
            WorkItemUpdated updated => (updated.Id, updated.Revision),
            _ => (Guid.Empty, null),
        };
        if (workItemId == Guid.Empty || await CreatorAsync(workItemId, revision, cancellationToken) is not { } creator)
        {
            return;
        }

        var outcome = await starter.StartAsync(workItemId, creator, null, cancellationToken);
        if (outcome.Block is { } block)
        {
            logger.LogDebug("No agent session for work item {WorkItemId}: {Block}", workItemId, block);
        }
    }

    private async Task<Guid?> CreatorAsync(Guid workItemId, int? revision, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TriggerRow>(new CommandDefinition(
            """
            SELECT s.starts_agent AS startsAgent, w.original_system AS itemOriginalSystem, w.created_by AS createdBy,
                   r.changed_by AS changedBy, r.changed_at AS changedAt, r.original_system AS revisionOriginalSystem,
                   coalesce(r.changes @> '[{"field": "state"}]'::jsonb, false) AS stateChanged
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            LEFT JOIN revision r ON r.work_item_id = w.id AND r.rev = @revision
            WHERE w.id = @workItemId
            """,
            new { workItemId, revision }, cancellationToken: cancellationToken));
        if (row is not { StartsAgent: true })
        {
            return null;
        }

        if (revision is null)
        {
            return row.ItemOriginalSystem is null ? row.CreatedBy : null;
        }

        var byPerson = row.RevisionOriginalSystem is null && row.StateChanged;
        return byPerson && row.ChangedAt is { } at && time.GetUtcNow() - at <= Recent ? row.ChangedBy : null;
    }

    private sealed record TriggerRow(
        bool StartsAgent, string? ItemOriginalSystem, Guid? CreatedBy,
        Guid? ChangedBy, DateTimeOffset? ChangedAt, string? RevisionOriginalSystem, bool StateChanged);
}
