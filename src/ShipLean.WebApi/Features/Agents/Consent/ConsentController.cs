using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Consent;

// The consent page's two calls, for the signed-in user. The cookie only: a bearer is never accepted on /api. A request
// that is unknown, expired or another user's is not_found.
[Route("api/agents/consent")]
public sealed class ConsentController(IAgentConsent consent, ICurrentUser currentUser, IUsageEvents events, IUsageScope scope, TimeProvider time) : ApiControllerBase
{
    private const int MaxClientNameLength = 64;

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ConsentRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<ConsentRequestDto> Get(Guid id, CancellationToken cancellationToken) => consent.GetAsync(id, cancellationToken);

    [HttpPost("{id:guid}")]
    [ProducesResponseType<ConsentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ConsentResult> Decide(Guid id, [FromBody] ConsentDecision decision, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        // Read first: the request is spent by the decision. Unknown, expired and another user's are the same NotFound.
        var request = await consent.GetAsync(id, cancellationToken);
        var result = await consent.DecideAsync(id, decision, cancellationToken);
        var clientName = request.ClientName.Length > MaxClientNameLength ? request.ClientName[..MaxClientNameLength] : request.ClientName;
        SelfEmittedUsage.TrackOk(
            events,
            scope,
            time,
            started,
            decision.Allow ? UsageEventNames.AgentConnected : UsageEventNames.AgentConsentDenied,
            currentUser.Id,
            "api:Consent.Decide",
            properties: new Dictionary<string, object> { [UsageProps.AgentName] = clientName });
        return result;
    }
}
