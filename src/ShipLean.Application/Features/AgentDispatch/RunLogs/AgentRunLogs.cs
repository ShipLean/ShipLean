using Microsoft.AspNetCore.Http;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.AgentDispatch.RunLogs;

// POST api/uploads/agent-log?token=: an agent's run log through its session's agentLog transfer link.
public sealed class AgentRunLogs : IAgentRunLogs
{
    // TODO: fails closed until the real upload (claim the token, store agent-run-<session id>.log, set run_log_attachment_id) replaces it.
    public Task UploadAsync(string token, HttpRequest request, CancellationToken cancellationToken = default)
        => throw ApiException.NotFound("This upload link isn't valid.");
}
