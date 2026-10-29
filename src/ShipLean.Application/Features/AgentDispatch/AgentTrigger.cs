using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.AgentDispatch;

// Decides whether a work item event starts a session, and starts it. The bus is in process and not durable: a session lost
// between commit and this handler is what the panel's Start agent is for.
// A created item starts one if it lands in a flagged state and was not imported. A moved or updated item starts one when the
// revision the event names changed its state (so a reorder inside the state does not), that revision is by a person (no import,
// webhook, named MCP agent or bulk write: a settings save, rollover or backlog move), it is recent (a revision from long ago is not
// "entering" the state now), and the state it moved the item into starts an agent. An MCP agent moving an item into a flagged
// state starts nothing (conductor decision). The person is the session's creator. No live session, connection and repository are checked by the starter.
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

        var outcome = await starter.StartAsync(workItemId, creator, null, null, null, cancellationToken);
        if (outcome.Block is { } block)
        {
            logger.LogDebug("No agent session for work item {WorkItemId}: {Block}", workItemId, block);
        }
    }

    private async Task<Guid?> CreatorAsync(Guid workItemId, int? revision, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // The state is the one the revision moved the item into (the "to" of its state change), not the state the item is in now;
        // a created item (revision 0 here, real revisions start at 1) is in the state it was created in.
        var row = await connection.QuerySingleOrDefaultAsync<TriggerRow>(new CommandDefinition(
            """
            SELECT s.starts_agent AS startsAgent, w.original_system AS itemOriginalSystem, w.created_by AS createdBy,
                   r.changed_by AS changedBy, r.changed_at AS changedAt, r.original_system AS revisionOriginalSystem,
                   r.webhook AS webhook, r.agent_name AS agentName, coalesce(r.bulk, false) AS bulk
            FROM work_item w
            LEFT JOIN revision r ON r.work_item_id = w.id AND r.rev = @revision
            LEFT JOIN state s ON (@revision = 0 AND s.id = w.state_id)
                OR (@revision > 0 AND s.id::text = (SELECT c ->> 'to' FROM jsonb_array_elements(r.changes) c WHERE c ->> 'field' = 'state' LIMIT 1))
            WHERE w.id = @workItemId
            """,
            new { workItemId, revision = revision ?? 0 }, cancellationToken: cancellationToken));
        if (row is not { StartsAgent: true })
        {
            return null;
        }

        if (revision is null)
        {
            return row.ItemOriginalSystem is null ? row.CreatedBy : null;
        }

        // A person through the app: not an import, a webhook, an agent (named or not, an MCP agent stamps its name) or a bulk write.
        var byPerson = row.RevisionOriginalSystem is null && row.Webhook is null && row.AgentName is null && !row.Bulk && row.ChangedBy is not null;
        return byPerson && row.ChangedAt is { } at && time.GetUtcNow() - at <= Recent ? row.ChangedBy : null;
    }

    private sealed record TriggerRow(
        bool StartsAgent, string? ItemOriginalSystem, Guid? CreatedBy,
        Guid? ChangedBy, DateTimeOffset? ChangedAt, string? RevisionOriginalSystem, string? Webhook, string? AgentName, bool Bulk);
}
