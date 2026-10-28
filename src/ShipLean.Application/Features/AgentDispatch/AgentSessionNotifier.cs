using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.AgentDispatch;

// The one place that says "this session or its activity changed". B3 calls it after every commit that changes a session
// (a session failed or canceled by the delivery service, a prompt queued by a comment or Stop). C2 calls it after its own commits.
// TODO: D1 publishes AgentSessionChanged(ProjectId, WorkItemId, SessionId) here (look the ids up from the session row).
public sealed class AgentSessionNotifier(ILogger<AgentSessionNotifier> logger)
{
    public Task ChangedAsync(Guid sessionId)
    {
        logger.LogDebug("Agent session {SessionId} changed", sessionId);
        return Task.CompletedTask;
    }
}
