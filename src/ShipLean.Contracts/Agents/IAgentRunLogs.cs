using Microsoft.AspNetCore.Http;

namespace ShipLean.Contracts.Agents;

// POST api/uploads/agent-log?token=: an agent's run log (text/plain, at most 1 MiB) through its session's one-use agentLog
// transfer link. Stored as the attachment agent-run-<session id>.log on the item and set as the session's run log. An unknown,
// spent, expired or revoked token is ApiException.NotFound("This upload link isn't valid.").
public interface IAgentRunLogs
{
    Task UploadAsync(string token, HttpRequest request, CancellationToken cancellationToken = default);
}
