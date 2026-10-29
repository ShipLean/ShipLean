using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch.Rendering;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.AgentDispatch;

// Decides whether a work item event starts a session, and starts it. The bus is in process and not durable: a session lost
// between commit and this handler is what the panel's Start agent is for.
// Only a WorkItemUpdated whose fields include the state counts: every state change publishes one from PersistAsync (a column drag too).
// A WorkItemMoved is ignored, because a re-order publishes it with an old revision. A created item starts nothing.
// The revision the event names gives the move (the "from" and "to" of its state change); the type and project come from the item. The
// board of the type's level holds the transitions; they are normalised against the project's states, the Invalid ones are skipped, and an
// exact from match beats Any, so one move starts at most one session, with the first Send to agent action of that transition.
// Gates: the revision is by a person (no import, webhook, named MCP agent or bulk write), it is recent (a revision from long ago is not
// "entering" the state now). The person is the session's creator. No live session, connection and repository are checked by the starter.
public sealed class AgentTrigger(NpgsqlDataSource dataSource, AgentSessionStarter starter, TimeProvider time, ILogger<AgentTrigger> logger)
{
    private const string AgentKind = "agent";
    private static readonly TimeSpan Recent = TimeSpan.FromMinutes(5);

    public async Task HandleAsync(ProjectEvent projectEvent, CancellationToken cancellationToken = default)
    {
        if (projectEvent is not WorkItemUpdated updated || !updated.Fields.Contains("state"))
        {
            return;
        }

        if (await MatchAsync(updated, cancellationToken) is not { } match)
        {
            return;
        }

        var (creator, transition, action) = match;

        var outcome = await starter.StartAsync(
            updated.Id, creator, null, action.ConnectionId, action.Prompt, cancellationToken,
            new AgentTransitionInput(transition.From?.Name ?? "Any", transition.To.Name));
        if (outcome.Block is { } block)
        {
            logger.LogDebug("No agent session for work item {WorkItemId}: {Block}", updated.Id, block);
        }
    }

    private async Task<(Guid Creator, TransitionJson Transition, ActionJson Action)?> MatchAsync(WorkItemUpdated updated, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TriggerRow>(new CommandDefinition(
            """
            SELECT w.type AS type, r.changed_by AS changedBy, r.changed_at AS changedAt, r.original_system AS revisionOriginalSystem,
                   r.webhook AS webhook, r.agent_name AS agentName, coalesce(r.bulk, false) AS bulk, r.changes::text AS changes
            FROM work_item w
            JOIN revision r ON r.work_item_id = w.id AND r.rev = @revision
            WHERE w.id = @workItemId
            """,
            new { workItemId = updated.Id, revision = updated.Revision }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        // A person through the app: not an import, a webhook, an agent (named or not, an MCP agent stamps its name) or a bulk write.
        var byPerson = row.RevisionOriginalSystem is null && row.Webhook is null && row.AgentName is null && !row.Bulk && row.ChangedBy is not null;
        if (!byPerson || row.ChangedAt is not { } at || time.GetUtcNow() - at > Recent || StateChange(row.Changes) is not { } move)
        {
            return null;
        }

        var (from, to) = move;
        var type = WorkItemTypes.Parse(row.Type);
        if (WorkItemTypes.BoardLevel(type) is not { } level)
        {
            return null;
        }

        var json = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT settings::text FROM board WHERE project_id = @projectId AND level = @level",
            new { projectId = updated.ProjectId, level }, cancellationToken: cancellationToken));
        if (json is null)
        {
            return null;
        }

        var board = JsonSerializer.Deserialize<BoardSettingsJson>(json, PersistedJson.Options)!;
        if (board.Transitions.Count == 0)
        {
            return null;
        }

        var states = (await connection.QueryAsync<StateRow>(new CommandDefinition(
            "SELECT id, type, name, archived_at IS NOT NULL AS archived FROM state WHERE project_id = @projectId",
            new { projectId = updated.ProjectId }, cancellationToken: cancellationToken)))
            .Select(s => new TransitionState(s.Id, WorkItemTypes.Parse(s.Type), s.Name, s.Archived)).ToList();
        var live = states.Where(s => !s.Archived).ToList();
        var matching = TransitionStates.Normalize(board.Transitions, states)
            .Where(t => t.Type == type && t.To.StateId == to && (t.From is null || t.From.StateId == from) && TransitionStates.IsValid(t, live))
            .ToList();
        var transition = matching.FirstOrDefault(t => t.From is not null) ?? matching.FirstOrDefault();
        return transition?.Actions.FirstOrDefault(a => a.Kind == AgentKind) is { } action ? (row.ChangedBy!.Value, transition, action) : null;
    }

    private static (Guid From, Guid To)? StateChange(string changes)
    {
        foreach (var change in JsonSerializer.Deserialize<JsonElement>(changes).EnumerateArray())
        {
            if (change.GetProperty("field").GetString() == "state"
                && Guid.TryParse(change.GetProperty("from").GetString(), out var from) && Guid.TryParse(change.GetProperty("to").GetString(), out var to))
            {
                return (from, to);
            }
        }

        return null;
    }

    private sealed record TriggerRow(
        string Type, Guid? ChangedBy, DateTimeOffset? ChangedAt, string? RevisionOriginalSystem, string? Webhook, string? AgentName, bool Bulk, string Changes);

    private sealed record StateRow(Guid Id, string Type, string Name, bool Archived);
}
