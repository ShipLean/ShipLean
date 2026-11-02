using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.AgentDispatch;

// The one place that says "this session or its activity changed". Every commit that changes a session or writes an activity
// calls it afterwards (session creation, B3's deliveries and prompts, C2's mutations). It publishes AgentSessionChanged so open
// work item pages refetch their Agent panel. The commit has already happened, so a failure here is logged and never thrown.
// Background callers run under ExecutionContext.SuppressFlow(), so the event carries no tab's client id.
// A caller whose commit moved the session to a terminal state passes that state as endedState, and the notifier tracks agent_session.finished once.
public sealed class AgentSessionNotifier(NpgsqlDataSource dataSource, IProjectEvents events, IUsageEvents usage, TimeProvider time, ILogger<AgentSessionNotifier> logger)
{
    public async Task ChangedAsync(Guid sessionId, string? endedState = null)
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
                if (endedState is not null)
                {
                    TrackFinished(ids.ProjectId, endedState);
                }
            }
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Could not publish AgentSessionChanged for agent session {SessionId}", sessionId);
        }
    }

    private void TrackFinished(Guid projectId, string endedState)
    {
        var outcome = endedState switch
        {
            AgentSessionStates.Completed => UsageOutcome.Ok,
            AgentSessionStates.Failed => UsageOutcome.Error,
            _ => UsageOutcome.Cancelled,
        };
        usage.Track(new UsageEvent(
            UsageEventNames.AgentSessionFinished, UsageSurface.System, UsageActor.System, projectId, 1, outcome, null, null,
            $"system:{nameof(AgentSessionNotifier)}", time.GetUtcNow(), new Dictionary<string, object>()));
    }

    private sealed record SessionIds(Guid ProjectId, Guid WorkItemId);
}
