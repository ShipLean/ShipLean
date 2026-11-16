using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.WorkItemDetails.Attachments;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.AgentDispatch;

// An agent's `curl -X POST -H 'Content-Type: text/plain' --data-binary @run.log <url>`: the last lines of its own output for a session that ended with
// an error. Anonymous: the one-use agentLog token in the query is the credential (no cookie, bearer or client header), and it is rate limited per IP.
// The token is in the query on purpose: the path reaches the request log, the query string does not.
[AllowAnonymous]
[EnableRateLimiting(UploadServices.RateLimit)]
[Route("api/uploads/agent-log")]
public sealed class AgentLogUploadController(IAgentRunLogs runLogs) : ApiControllerBase
{
    [HttpPost]
    [StreamedUpload]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task Upload([FromQuery] string? token, CancellationToken cancellationToken)
        => runLogs.UploadAsync(token ?? string.Empty, Request, cancellationToken);
}
