using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.AgentDispatch;

// The one place that says "this session or its activity changed". Every commit that changes a session or writes an activity
// calls it afterwards (session creation, B3's deliveries and prompts, C2's mutations). It publishes AgentSessionChanged so open
// work item pages refetch their Agent panel. The commit has already happened, so a failure here is logged and never thrown.
// Background callers run under ExecutionContext.SuppressFlow(), so the event carries no tab's client id.
public sealed class AgentSessionNotifier(NpgsqlDataSource dataSource, IProjectEvents events, ILogger<AgentSessionNotifier> logger)
{
    public async Task ChangedAsync(Guid sessionId)
    {
        logger.LogDebug("Agent session {SessionId} changed", sessionId);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            var ids = await connection.QuerySingleOrDefaultAsync<SessionIds>(new CommandDefinition(
                """
                SELECT w.project_id AS projectId, s.work_item_id AS workItemId
                FROM agent_session s JOIN work_item w ON w.id = s.work_item_id
                WHERE s.id = @sessionId
                """,
                new { sessionId }));
            if (ids is not null)
            {
                await events.PublishAsync(new AgentSessionChanged(ids.ProjectId, ids.WorkItemId, sessionId));
            }
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Could not publish AgentSessionChanged for agent session {SessionId}", sessionId);
        }
    }

    private sealed record SessionIds(Guid ProjectId, Guid WorkItemId);
}
